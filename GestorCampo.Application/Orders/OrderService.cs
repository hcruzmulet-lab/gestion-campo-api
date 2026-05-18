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

        var order = new Order
        {
            ClientId = request.ClientId,
            Client = client,
            VendorId = currentUserId,
            Vendor = null!,
            VisitId = request.VisitId,
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

        if (currentRole == UserRole.Vendor && order.VendorId != currentUserId)
            return ServiceResult<OrderResponse>.Fail("No tiene acceso a esta orden");

        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<PagedResult<OrderResponse>>> GetListAsync(
        OrderListRequest request, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var vendorFilter = currentRole == UserRole.Vendor ? currentUserId : request.VendorId;

        var (items, totalCount) = await _orders.GetListAsync(
            request.Page, pageSize,
            request.Status, vendorFilter, request.ClientId, request.VisitId,
            request.From, request.To, ct);

        return ServiceResult<PagedResult<OrderResponse>>.Ok(new PagedResult<OrderResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = pageSize
        });
    }

    public async Task<ServiceResult<OrderResponse>> SendAsync(
        Guid id, Guid currentUserId, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (order.Status != OrderStatus.Draft)
            return ServiceResult<OrderResponse>.Fail("Solo se pueden enviar órdenes en borrador");

        order.Status = OrderStatus.Sent;
        order.UpdatedBy = currentUserId;
        await _orders.UpdateAsync(order, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> ApproveAsync(
        Guid id, Guid currentUserId, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (order.Status != OrderStatus.Sent)
            return ServiceResult<OrderResponse>.Fail("Solo se pueden aprobar órdenes enviadas");

        order.Status = OrderStatus.Approved;
        order.ApprovedAt = DateTime.UtcNow;
        order.UpdatedBy = currentUserId;
        await _orders.UpdateAsync(order, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> RejectAsync(
        Guid id, RejectOrderRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (order.Status != OrderStatus.Sent)
            return ServiceResult<OrderResponse>.Fail("Solo se pueden rechazar órdenes enviadas");

        order.Status = OrderStatus.Rejected;
        order.RejectionComment = request.Comment;
        order.UpdatedBy = currentUserId;
        await _orders.UpdateAsync(order, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> DeliverAsync(
        Guid id, Guid currentUserId, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order == null)
            return ServiceResult<OrderResponse>.Fail("Orden no encontrada");

        if (order.Status != OrderStatus.Approved)
            return ServiceResult<OrderResponse>.Fail("Solo se pueden entregar órdenes aprobadas");

        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.UtcNow;
        order.UpdatedBy = currentUserId;
        await _orders.UpdateAsync(order, ct);
        return ServiceResult<OrderResponse>.Ok(ToResponse(order));
    }

    private static OrderResponse ToResponse(Order o) => new()
    {
        Id = o.Id,
        ClientId = o.ClientId,
        ClientName = o.Client?.Name ?? string.Empty,
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
