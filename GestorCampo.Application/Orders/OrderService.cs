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
        foreach (var lineReq in request.Lines)
        {
            var product = await _products.GetByIdAsync(lineReq.ProductId, ct);
            if (product == null)
                return ServiceResult<OrderResponse>.Fail($"Producto no encontrado: {lineReq.ProductId}");

            lines.Add(new OrderLine
            {
                ProductId = lineReq.ProductId,
                Product = product,
                Quantity = lineReq.Quantity,
                UnitPrice = lineReq.UnitPrice,
                Discount = lineReq.Discount
            });
        }

        // Auto-approve at create time. The mobile vendor flow no longer goes
        // through a Supervisor approval queue: if the create request reached
        // the API, the vendor is online by definition, so the order skips
        // Draft/Sent and lands as Approved. Offline-created drafts stay as
        // Draft locally on the device until the outbox drains, at which point
        // this same code path approves them.
        var order = new Order
        {
            ClientId = request.ClientId,
            Client = client,
            VendorId = currentUserId,
            Vendor = null!,
            VisitId = request.VisitId,
            // New orders start as Draft so the vendor can iterate on lines /
            // quantities (online or offline) until they explicitly hit Send,
            // which transitions Draft -> Approved (see SendAsync). Without
            // this, an offline edit on a fresh order races against the
            // server's auto-Approved state and the PUT lands in conflict
            // with 409 "Solo se pueden editar órdenes en borrador".
            Status = OrderStatus.Draft,
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

        // No Supervisor approval step anymore — sending a draft approves it
        // directly. The Sent state is reserved for legacy data; new orders
        // never enter it.
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
