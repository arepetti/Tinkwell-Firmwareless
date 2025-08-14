using FluentAssertions;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Repositories;

namespace Tinkwell.Firmwareless.PublicRepository.UnitTests;

public class ProductsServiceTests
{
    [Fact]
    public async Task CreateAsync_AsUser_ForOwnVendor_ShouldSucceed()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new ProductsService(dbContext);
        var vendor = new Vendor { Id = Guid.NewGuid(), Name = "Test Vendor" };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        var userPrincipal = CreatePrincipal("User", [Scopes.ProductCreate], vendor.Id);
        var request = new ProductsService.CreateRequest(vendor.Id, "Test Product", "T-1000", ProductStatus.Production);

        // Act
        var result = await service.CreateAsync(userPrincipal, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Product");
        result.VendorId.Should().Be(vendor.Id);
    }

    [Fact]
    public async Task CreateAsync_AsUser_ForAnotherVendor_ShouldThrowForbidden()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new ProductsService(dbContext);
        var myVendor = new Vendor { Id = Guid.NewGuid(), Name = "My Vendor" };
        var otherVendor = new Vendor { Id = Guid.NewGuid(), Name = "Other Vendor" };
        dbContext.Vendors.AddRange(myVendor, otherVendor);
        await dbContext.SaveChangesAsync();
        var userPrincipal = CreatePrincipal("User", [Scopes.ProductCreate], myVendor.Id);
        var request = new ProductsService.CreateRequest(otherVendor.Id, "Test Product", "T-1000", ProductStatus.Production);

        // Act
        var action = async () => await service.CreateAsync(userPrincipal, request, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task FindAllAsync_AsUser_ShouldReturnOnlyOwnProducts()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new ProductsService(dbContext);
        var myVendor = new Vendor { Id = Guid.NewGuid(), Name = "My Vendor" };
        var otherVendor = new Vendor { Id = Guid.NewGuid(), Name = "Other Vendor" };
        var myProduct = new Product { Name = "My Product", Model = "P-1", Vendor = myVendor };
        var otherProduct = new Product { Name = "Other Product", Model = "P-2", Vendor = otherVendor };
        dbContext.Products.AddRange(myProduct, otherProduct);
        await dbContext.SaveChangesAsync();
        var userPrincipal = CreatePrincipal("User", [Scopes.ProductRead], myVendor.Id);
        var request = new Services.Queries.FindRequest(0, 20, null, null);

        // Act
        var result = await service.FindAllAsync(userPrincipal, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.Single().Id.Should().Be(myProduct.Id);
    }

    private ClaimsPrincipal CreatePrincipal(string role, string[] scopes, Guid? vendorId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        claims.AddRange(scopes.Select(s => new Claim("scope", s)));
        if (vendorId.HasValue) { claims.Add(new Claim(CustomClaimTypes.VendorId, vendorId.Value.ToString())); }
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }
}
