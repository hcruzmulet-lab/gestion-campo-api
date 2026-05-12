using FluentAssertions;
using GestorCampo.Application.Products;
using GestorCampo.Application.Products.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using Moq;

namespace GestorCampo.Tests.Products;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _sut = new ProductService(_productRepo.Object);
    }

    private Product BuildProduct() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Producto A",
        Code = "PROD-001",
        Price = 10.50m,
        IsActive = true
    };

    [Fact]
    public async Task Create_DuplicateCode_ReturnsFail()
    {
        _productRepo.Setup(r => r.CodeExistsAsync("PROD-001", default)).ReturnsAsync(true);

        var result = await _sut.CreateAsync(
            new CreateProductRequest { Code = "PROD-001", Name = "A", Price = 10m },
            Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("código");
    }

    [Fact]
    public async Task Create_ValidData_SavesAndReturnsProduct()
    {
        _productRepo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), default)).ReturnsAsync(false);

        var result = await _sut.CreateAsync(
            new CreateProductRequest { Code = "P001", Name = "Shampoo X", Price = 5.99m },
            Guid.NewGuid());

        result.Succeeded.Should().BeTrue();
        result.Data!.Code.Should().Be("P001");
        result.Data.Price.Should().Be(5.99m);
        _productRepo.Verify(r => r.AddAsync(It.IsAny<Product>(), default), Times.Once);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsFail()
    {
        _productRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Product?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_Found_ReturnsProduct()
    {
        var product = BuildProduct();
        _productRepo.Setup(r => r.GetByIdAsync(product.Id, default)).ReturnsAsync(product);

        var result = await _sut.GetByIdAsync(product.Id);

        result.Succeeded.Should().BeTrue();
        result.Data!.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task GetList_PassesFiltersToRepository()
    {
        _productRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product>(), 0));

        await _sut.GetListAsync(new ProductListRequest { Search = "shampoo", IsActive = true });

        _productRepo.Verify(r => r.GetListAsync(1, 20, "shampoo", true, null, default), Times.Once);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsFail()
    {
        _productRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Product?)null);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateProductRequest(), Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ValidData_UpdatesNameAndPrice()
    {
        var product = BuildProduct();
        _productRepo.Setup(r => r.GetByIdAsync(product.Id, default)).ReturnsAsync(product);

        var result = await _sut.UpdateAsync(product.Id, new UpdateProductRequest { Name = "New Name", Price = 99m }, Guid.NewGuid());

        result.Succeeded.Should().BeTrue();
        result.Data!.Name.Should().Be("New Name");
        result.Data.Price.Should().Be(99m);
        _productRepo.Verify(r => r.UpdateAsync(
            It.Is<Product>(p => p.Name == "New Name" && p.Price == 99m), default), Times.Once);
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsFail()
    {
        _productRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Product?)null);

        var result = await _sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ValidProduct_SoftDeletesAndDeactivates()
    {
        var product = BuildProduct();
        _productRepo.Setup(r => r.GetByIdAsync(product.Id, default)).ReturnsAsync(product);

        var result = await _sut.DeleteAsync(product.Id, Guid.NewGuid());

        result.Succeeded.Should().BeTrue();
        _productRepo.Verify(r => r.UpdateAsync(
            It.Is<Product>(p => p.DeletedAt.HasValue && !p.IsActive), default), Times.Once);
    }
}
