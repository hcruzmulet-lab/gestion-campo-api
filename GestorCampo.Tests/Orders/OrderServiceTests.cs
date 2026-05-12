using FluentAssertions;
using GestorCampo.Application.Orders;
using GestorCampo.Application.Orders.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using Moq;

namespace GestorCampo.Tests.Orders;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IClientRepository> _clientRepo = new();
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _sut = new OrderService(_orderRepo.Object, _clientRepo.Object, _productRepo.Object);
    }

    private Client BuildClient() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Farmacia Test",
        TaxId = "0912345678001",
        Address = "Av. Test 123",
        Phone = "0991234567",
        Email = "farmacia@test.com",
        IsActive = true
    };

    private Product BuildProduct() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Producto A",
        Code = "PROD-001",
        Price = 10.50m,
        IsActive = true
    };

    private Order BuildOrder(Guid? vendorId = null, OrderStatus status = OrderStatus.Draft) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = Guid.NewGuid(),
        Client = BuildClient(),
        VendorId = vendorId ?? Guid.NewGuid(),
        Vendor = new User { Id = Guid.NewGuid(), Name = "V", Email = "v@t.com", PasswordHash = "h", Role = UserRole.Vendor },
        Status = status,
        IsActive = true,
        Lines = new List<OrderLine>()
    };

    // --- Create ---

    [Fact]
    public async Task Create_EmptyLines_ReturnsFail()
    {
        var result = await _sut.CreateAsync(
            new CreateOrderRequest { ClientId = Guid.NewGuid(), Lines = new List<CreateOrderLineRequest>() },
            Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("La orden debe tener al menos una línea");
    }

    [Fact]
    public async Task Create_ClientNotFound_ReturnsFail()
    {
        var productId = Guid.NewGuid();
        _clientRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Client?)null);

        var result = await _sut.CreateAsync(
            new CreateOrderRequest
            {
                ClientId = Guid.NewGuid(),
                Lines = new List<CreateOrderLineRequest>
                {
                    new() { ProductId = productId, Quantity = 1, UnitPrice = 10m, Discount = 0m }
                }
            },
            Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Cliente no encontrado");
    }

    [Fact]
    public async Task Create_ValidOrder_ReturnsOk()
    {
        var currentUserId = Guid.NewGuid();
        var client = BuildClient();
        var product = BuildProduct();
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);
        _productRepo.Setup(r => r.GetByIdAsync(product.Id, default)).ReturnsAsync(product);

        var result = await _sut.CreateAsync(
            new CreateOrderRequest
            {
                ClientId = client.Id,
                Lines = new List<CreateOrderLineRequest>
                {
                    new() { ProductId = product.Id, Quantity = 2, UnitPrice = 10.50m, Discount = 0.1m }
                }
            },
            currentUserId);

        result.Succeeded.Should().BeTrue();
        result.Data!.VendorId.Should().Be(currentUserId);
        result.Data.Status.Should().Be(OrderStatus.Draft);
        result.Data.Lines.Should().HaveCount(1);
        _orderRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), default), Times.Once);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_NotFound_ReturnsFail()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Order?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Orden no encontrada");
    }

    [Fact]
    public async Task GetById_VendorOwnOrder_ReturnsOk()
    {
        var vendorId = Guid.NewGuid();
        var order = BuildOrder(vendorId: vendorId);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var result = await _sut.GetByIdAsync(order.Id, vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        result.Data!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetById_VendorOtherOrder_ReturnsFail()
    {
        var order = BuildOrder(vendorId: Guid.NewGuid());
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var result = await _sut.GetByIdAsync(order.Id, Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta orden");
    }

    // --- Send ---

    [Fact]
    public async Task Send_NotFound_ReturnsFail()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Order?)null);

        var result = await _sut.SendAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Orden no encontrada");
    }

    [Fact]
    public async Task Send_NotDraft_ReturnsFail()
    {
        var order = BuildOrder(status: OrderStatus.Sent);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var result = await _sut.SendAsync(order.Id, Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Solo se pueden enviar órdenes en borrador");
    }

    [Fact]
    public async Task Send_ValidDraft_ReturnsSent()
    {
        var currentUserId = Guid.NewGuid();
        var order = BuildOrder(status: OrderStatus.Draft);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var result = await _sut.SendAsync(order.Id, currentUserId);

        result.Succeeded.Should().BeTrue();
        _orderRepo.Verify(r => r.UpdateAsync(
            It.Is<Order>(o => o.Status == OrderStatus.Sent && o.UpdatedBy == currentUserId),
            default), Times.Once);
    }

    // --- Approve ---

    [Fact]
    public async Task Approve_NotFound_ReturnsFail()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Order?)null);

        var result = await _sut.ApproveAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Orden no encontrada");
    }

    [Fact]
    public async Task Approve_NotSent_ReturnsFail()
    {
        var order = BuildOrder(status: OrderStatus.Draft);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var result = await _sut.ApproveAsync(order.Id, Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Solo se pueden aprobar órdenes enviadas");
    }

    [Fact]
    public async Task Approve_ValidSent_ReturnsApprovedWithTimestamp()
    {
        var currentUserId = Guid.NewGuid();
        var order = BuildOrder(status: OrderStatus.Sent);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var result = await _sut.ApproveAsync(order.Id, currentUserId);

        result.Succeeded.Should().BeTrue();
        _orderRepo.Verify(r => r.UpdateAsync(
            It.Is<Order>(o =>
                o.Status == OrderStatus.Approved &&
                o.ApprovedAt.HasValue &&
                o.UpdatedBy == currentUserId),
            default), Times.Once);
    }

    // --- Reject ---

    [Fact]
    public async Task Reject_ValidSent_SetsRejectedAndComment()
    {
        var currentUserId = Guid.NewGuid();
        var order = BuildOrder(status: OrderStatus.Sent);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var result = await _sut.RejectAsync(order.Id, new RejectOrderRequest { Comment = "Precio incorrecto" }, currentUserId);

        result.Succeeded.Should().BeTrue();
        _orderRepo.Verify(r => r.UpdateAsync(
            It.Is<Order>(o =>
                o.Status == OrderStatus.Rejected &&
                o.RejectionComment == "Precio incorrecto" &&
                o.UpdatedBy == currentUserId),
            default), Times.Once);
    }

    // --- Deliver ---

    [Fact]
    public async Task Deliver_ValidApproved_ReturnsDelivered()
    {
        var currentUserId = Guid.NewGuid();
        var order = BuildOrder(status: OrderStatus.Approved);
        _orderRepo.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

        var result = await _sut.DeliverAsync(order.Id, currentUserId);

        result.Succeeded.Should().BeTrue();
        _orderRepo.Verify(r => r.UpdateAsync(
            It.Is<Order>(o =>
                o.Status == OrderStatus.Delivered &&
                o.DeliveredAt.HasValue &&
                o.UpdatedBy == currentUserId),
            default), Times.Once);
    }
}
