using FluentAssertions;
using GestorCampo.Application.Sync;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Providers;
using GestorCampo.Domain.Interfaces.Repositories;
using GestorCampo.Domain.Models;
using Moq;

namespace GestorCampo.Tests.Sync;

public class SyncServiceTests
{
    private readonly Mock<IClientProvider> _clientProvider = new();
    private readonly Mock<IProductProvider> _productProvider = new();
    private readonly Mock<IOrderExporter> _orderExporter = new();
    private readonly Mock<IClientRepository> _clients = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<ISyncLogRepository> _syncLogs = new();
    private readonly SyncService _sut;

    public SyncServiceTests()
    {
        _sut = new SyncService(
            _clientProvider.Object,
            _productProvider.Object,
            _orderExporter.Object,
            _clients.Object,
            _products.Object,
            _syncLogs.Object);
    }

    // --- SyncClients ---

    [Fact]
    public async Task SyncClients_NoItems_RecordsSuccess()
    {
        _clientProvider.Setup(p => p.FetchAsync(default)).ReturnsAsync(new List<ExternalClientData>());

        await _sut.SyncClientsAsync();

        _syncLogs.Verify(r => r.UpdateAsync(
            It.Is<SyncLog>(l => l.Status == "success" && l.ItemsProcessed == 0),
            default), Times.Once);
    }

    [Fact]
    public async Task SyncClients_NewItem_CreatesClientAndRecordsSuccess()
    {
        var externalClient = new ExternalClientData
        {
            ExternalId = "EXT-001", Name = "Test Corp", TaxId = "0912345678001",
            Address = "Av. Test 123", Phone = "0991234567", Email = "test@corp.com"
        };
        _clientProvider.Setup(p => p.FetchAsync(default))
            .ReturnsAsync(new List<ExternalClientData> { externalClient });
        _clients.Setup(r => r.GetByExternalIdAsync("EXT-001", default))
            .ReturnsAsync((Client?)null);

        await _sut.SyncClientsAsync();

        _clients.Verify(r => r.AddAsync(
            It.Is<Client>(c => c.ExternalId == "EXT-001" && c.Source == "external"),
            default), Times.Once);
        _syncLogs.Verify(r => r.UpdateAsync(
            It.Is<SyncLog>(l => l.Status == "success" && l.ItemsProcessed == 1),
            default), Times.Once);
    }

    [Fact]
    public async Task SyncClients_ProviderThrows_RecordsFailure()
    {
        _clientProvider.Setup(p => p.FetchAsync(default))
            .ThrowsAsync(new Exception("Connection refused"));

        await _sut.SyncClientsAsync();

        _syncLogs.Verify(r => r.UpdateAsync(
            It.Is<SyncLog>(l => l.Status == "failed" && l.Error == "Connection refused"),
            default), Times.Once);
    }

    // --- SyncProducts ---

    [Fact]
    public async Task SyncProducts_NewItem_CreatesProductAndRecordsSuccess()
    {
        var externalProduct = new ExternalProductData
        {
            ExternalId = "P-001", Name = "Widget A", Code = "WGT-001", Price = 9.99m
        };
        _productProvider.Setup(p => p.FetchAsync(default))
            .ReturnsAsync(new List<ExternalProductData> { externalProduct });
        _products.Setup(r => r.GetByExternalIdAsync("P-001", default))
            .ReturnsAsync((Product?)null);

        await _sut.SyncProductsAsync();

        _products.Verify(r => r.AddAsync(
            It.Is<Product>(p => p.ExternalId == "P-001" && p.Source == "external"),
            default), Times.Once);
        _syncLogs.Verify(r => r.UpdateAsync(
            It.Is<SyncLog>(l => l.Status == "success" && l.ItemsProcessed == 1),
            default), Times.Once);
    }

    // --- ExportOrder ---

    [Fact]
    public async Task ExportOrder_ExporterSucceeds_RecordsSuccess()
    {
        var orderId = Guid.NewGuid();
        _orderExporter.Setup(e => e.ExportAsync(orderId, default)).ReturnsAsync(true);

        var result = await _sut.ExportOrderAsync(orderId);

        result.Should().BeTrue();
        _syncLogs.Verify(r => r.UpdateAsync(
            It.Is<SyncLog>(l => l.Status == "success" && l.ItemsProcessed == 1),
            default), Times.Once);
    }

    [Fact]
    public async Task ExportOrder_ExporterReturnsFalse_RecordsFailure()
    {
        var orderId = Guid.NewGuid();
        _orderExporter.Setup(e => e.ExportAsync(orderId, default)).ReturnsAsync(false);

        var result = await _sut.ExportOrderAsync(orderId);

        result.Should().BeFalse();
        _syncLogs.Verify(r => r.UpdateAsync(
            It.Is<SyncLog>(l => l.Status == "failed" && l.ItemsProcessed == 0),
            default), Times.Once);
    }
}
