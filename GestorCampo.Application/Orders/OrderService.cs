using GestorCampo.Application.Common;
using GestorCampo.Application.Orders.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;

namespace GestorCampo.Application.Orders;

public class OrderService
{
    private readonly IOrderRepository _orders;
    private readonly IClientRepository _clients;
    private readonly IProductRepository _products;

    public OrderService(IOrderRepository orders, IClientRepository clients, IProductRepository products)
    {
        _orders = orders;
        _clients = clients;
        _products = products;
    }

    public async Task<ServiceResult<OrderResponse>> CreateAsync(
        CreateOrderRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        if (request.Lines == null || request.Lines.Count == 0)
            return ServiceResult<OrderResponse>.Fail("La orden debe tener al menos una línea");

        var client = await _clients.GetByIdAsync(request.ClientId, ct);
        if (client == null)
            return ServiceResult<OrderResponse>.Fail("Cliente no encontrado");

        var lines = new List<OrderLine>();
        var stockLines = new List<(Product product, int quantity)>();
        foreach (var lineReq in request.Lines)
        {
            var product = await _products.GetByIdAsync(lineReq.ProductId, ct);
            if (product == null)
                return ServiceResult<OrderResponse>.Fail($"Producto no encontrado: {lineReq.ProductId}");

            stockLines.Add((product, lineReq.Quantity));
            lines.Add(new OrderLine
            {
                ProductId = lineReq.ProductId,
                Product = product,
                Quantity = lineReq.Quantity,
                UnitPrice = lineReq.UnitPrice,
                Discount = lineReq.Discount
            });
        }

        // Validate + decrement stock before the order lands. Insufficient
        // stock fails the whole create (nothing is persisted). Offline this
        // surfaces as a 409 conflict in the device's sync_failures.
        var stockError = ApplyStock(stockLines);
        if (stockError != null)
            return ServiceResult<OrderResponse>.Fail(stockError);

        // Auto-approve at create time. The mobile vendor flow builds every
        // line locally and sends them in a single POST, so there is no
        // create-then-edit step to protect: the order lands directly as
        // Approved ("activo"). Offline-created orders show Approved
        // optimistically and the API confirms that state when the outbox
        // drains. Stock is decremented here, atomically with the order write
        // (the decremented Product entities are tracked by the same DbContext
        // that AddAsync flushes).
        var order = new Order
        {
            ClientId = request.ClientId,
            Client = client,
            VendorId = currentUserId,
            Vendor = null!,
            VisitId = request.VisitId,
            Status = OrderStatus.Approved,
            ApprovedAt = DateTime.UtcNow,
            Lines = lines,
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId
        };

        await _orders.AddAsync(order, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> GetByIdAsync(
        Guid id, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (!HasAccess(order, currentUserId, currentRole))
            return ServiceResult<OrderResponse>.Fail("No tiene acceso a esta orden");

        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<PagedResult<OrderResponse>>> GetListAsync(
        OrderListRequest request, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var vendorFilter = currentRole == UserRole.Vendor ? currentUserId : request.VendorId;
        var supervisorFilter = currentRole == UserRole.Supervisor ? currentUserId : (Guid?)null;

        var (items, totalCount) = await _orders.GetListAsync(
            request.Page, pageSize,
            request.Status, vendorFilter, request.ClientId, request.VisitId,
            request.From, request.To,
            supervisorFilter,
            ct);

        return ServiceResult<PagedResult<OrderResponse>>.Ok(new PagedResult<OrderResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = pageSize
        });
    }

    public async Task<ServiceResult<OrderResponse>> UpdateAsync(
        Guid id, UpdateOrderRequest request, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (!HasAccess(order, currentUserId, currentRole))
            return ServiceResult<OrderResponse>.Fail("No tiene acceso a esta orden");

        if (order.Status != OrderStatus.Draft)
            return ServiceResult<OrderResponse>.Fail("Solo se pueden editar órdenes en borrador");

        if (request.Lines == null || request.Lines.Count == 0)
            return ServiceResult<OrderResponse>.Fail("La orden debe tener al menos una línea");

        var newLines = new List<OrderLine>();
        foreach (var lineReq in request.Lines)
        {
            var product = await _products.GetByIdAsync(lineReq.ProductId, ct);
            if (product == null)
                return ServiceResult<OrderResponse>.Fail($"Producto no encontrado: {lineReq.ProductId}");

            newLines.Add(new OrderLine
            {
                ProductId = lineReq.ProductId,
                Product = product,
                Quantity = lineReq.Quantity,
                UnitPrice = lineReq.UnitPrice,
                Discount = lineReq.Discount
            });
        }

        await _orders.ReplaceLinesAsync(order, newLines, currentUserId, ct);

        // Refresh entity so the returned response reflects the new lines.
        var refreshed = await _orders.GetByIdAsync(order.Id, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(refreshed ?? order));
    }

    public async Task<ServiceResult<OrderResponse>> SendAsync(
        Guid id, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (!HasAccess(order, currentUserId, currentRole))
            return ServiceResult<OrderResponse>.Fail("No tiene acceso a esta orden");

        if (order.Status != OrderStatus.Draft)
            return ServiceResult<OrderResponse>.Fail("Solo se pueden enviar órdenes en borrador");

        // Sending a (legacy) draft approves it directly — and is the point at
        // which its stock is decremented, mirroring CreateAsync. New orders
        // are auto-approved at create and never reach this path, so there is
        // no risk of decrementing twice.
        var stockError = ApplyStock(order.Lines.Select(l => (l.Product, l.Quantity)).ToList());
        if (stockError != null)
            return ServiceResult<OrderResponse>.Fail(stockError);

        order.Status = OrderStatus.Approved;
        order.ApprovedAt = DateTime.UtcNow;
        order.UpdatedBy = currentUserId;
        await _orders.UpdateAsync(order, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> ApproveAsync(
        Guid id, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (!HasAccess(order, currentUserId, currentRole))
            return ServiceResult<OrderResponse>.Fail("No tiene acceso a esta orden");

        if (order.Status != OrderStatus.Sent)
            return ServiceResult<OrderResponse>.Fail("Solo se pueden aprobar órdenes enviadas");

        order.Status = OrderStatus.Approved;
        order.ApprovedAt = DateTime.UtcNow;
        order.UpdatedBy = currentUserId;
        await _orders.UpdateAsync(order, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> RejectAsync(
        Guid id, RejectOrderRequest request, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (!HasAccess(order, currentUserId, currentRole))
            return ServiceResult<OrderResponse>.Fail("No tiene acceso a esta orden");

        if (order.Status != OrderStatus.Sent)
            return ServiceResult<OrderResponse>.Fail("Solo se pueden rechazar órdenes enviadas");

        order.Status = OrderStatus.Rejected;
        order.RejectionComment = request.Comment;
        order.UpdatedBy = currentUserId;
        await _orders.UpdateAsync(order, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid id, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult.Fail("Orden no encontrada");

        if (!HasAccess(order, currentUserId, currentRole))
            return ServiceResult.Fail("No tiene acceso a esta orden");

        // SuperAdmin can delete any status (cleanup / mistake recovery).
        // Everyone else is limited to Draft so an approved/sent order can't
        // be silently wiped by the vendor that created it.
        if (currentRole != UserRole.SuperAdmin && order.Status != OrderStatus.Draft)
            return ServiceResult.Fail("Solo se pueden eliminar órdenes en borrador");

        order.DeletedAt = DateTime.UtcNow;
        order.DeletedBy = currentUserId;
        order.IsActive = false;
        order.UpdatedBy = currentUserId;
        await _orders.UpdateAsync(order, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<BulkDeleteResult>> BulkDeleteAsync(
        IReadOnlyCollection<Guid> ids, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var deleted = new List<Guid>();
        var failed = new List<BulkDeleteFailure>();
        foreach (var id in ids.Distinct())
        {
            var r = await DeleteAsync(id, currentUserId, currentRole, ct);
            if (r.Succeeded) deleted.Add(id);
            else failed.Add(new BulkDeleteFailure(id, r.Error ?? "Error desconocido"));
        }
        return ServiceResult<BulkDeleteResult>.Ok(new BulkDeleteResult(deleted, failed));
    }

    public async Task<ServiceResult<OrderResponse>> DeliverAsync(
        Guid id, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (!HasAccess(order, currentUserId, currentRole))
            return ServiceResult<OrderResponse>.Fail("No tiene acceso a esta orden");

        if (order.Status != OrderStatus.Approved)
            return ServiceResult<OrderResponse>.Fail("Solo se pueden entregar órdenes aprobadas");

        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.UtcNow;
        order.UpdatedBy = currentUserId;
        await _orders.UpdateAsync(order, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    /// <summary>
    /// Validates stock availability and decrements it for the given lines.
    /// Quantities are aggregated per product so multiple lines of the same
    /// product are checked against the single stock figure. Products with a
    /// null Stock are "untracked" — skipped entirely (no validation, no
    /// decrement). Returns an error message if any product is short (in which
    /// case nothing is decremented), or null on success. Mutates the tracked
    /// Product entities; the caller's repository save persists the changes.
    /// </summary>
    private static string? ApplyStock(List<(Product product, int quantity)> lines)
    {
        var perProduct = lines
            .Where(l => l.product.Stock.HasValue)
            .GroupBy(l => l.product.Id)
            .Select(g => (product: g.First().product, total: g.Sum(x => x.quantity)))
            .ToList();

        foreach (var (product, total) in perProduct)
            if (product.Stock!.Value < total)
                return $"Stock insuficiente para {product.Name}: disponible {product.Stock.Value}, solicitado {total}";

        foreach (var (product, total) in perProduct)
            product.Stock = product.Stock!.Value - total;

        return null;
    }

    private static bool HasAccess(Order order, Guid currentUserId, UserRole role) => role switch
    {
        UserRole.SuperAdmin => true,
        UserRole.Vendor => order.VendorId == currentUserId,
        UserRole.Supervisor => order.Vendor != null && order.Vendor.SupervisorId == currentUserId,
        _ => false
    };

    private static OrderResponse ToResponse(Order o) => new()
    {
        Id = o.Id,
        ClientId = o.ClientId,
        ClientName = o.Client?.Name ?? string.Empty,
        ClientAddress = o.Client?.Address,
        VendorId = o.VendorId,
        VendorName = o.Vendor?.Name ?? string.Empty,
        VisitId = o.VisitId,
        Status = o.Status,
        RejectionComment = o.RejectionComment,
        ApprovedAt = o.ApprovedAt,
        DeliveredAt = o.DeliveredAt,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt,
        IsActive = o.IsActive,
        Lines = o.Lines.Select(l => new OrderLineResponse
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ProductName = l.Product?.Name ?? string.Empty,
            ProductCode = l.Product?.Code ?? string.Empty,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            Discount = l.Discount
        }).ToList()
    };
}
