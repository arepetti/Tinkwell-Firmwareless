using FluentAssertions;
using System.Security.Claims;
using Tinkwell.Firmwareless.Exceptions;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Services;

namespace Tinkwell.Firmwareless.PublicRepository.UnitTests;

public class VendorsServiceTests
{
    [Fact]
    public async Task FindAsync_AsAdmin_ShouldReturnVendor()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new VendorsService(dbContext);
        var vendor = new Vendor { Id = Guid.NewGuid(), Name = "Test Vendor" };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        var adminPrincipal = CreatePrincipal("Admin", [Scopes.VendorRead]);

        // Act
        var result = await service.FindAsync(adminPrincipal, vendor.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(vendor.Id);
    }

    [Fact]
    public async Task FindAsync_AsUser_ForOwnVendor_ShouldReturnVendor()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new VendorsService(dbContext);
        var vendor = new Vendor { Id = Guid.NewGuid(), Name = "Test Vendor" };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        var userPrincipal = CreatePrincipal("User", [Scopes.VendorRead], vendor.Id);

        // Act
        var result = await service.FindAsync(userPrincipal, vendor.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(vendor.Id);
    }

    [Fact]
    public async Task FindAsync_AsUser_ForAnotherVendor_ShouldThrowNotFound()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new VendorsService(dbContext);
        var myVendor = new Vendor { Id = Guid.NewGuid(), Name = "My Vendor" };
        var otherVendor = new Vendor { Id = Guid.NewGuid(), Name = "Other Vendor" };
        dbContext.Vendors.AddRange(myVendor, otherVendor);
        await dbContext.SaveChangesAsync();
        var userPrincipal = CreatePrincipal("User", [Scopes.VendorRead], myVendor.Id);

        // Act
        var action = async () => await service.FindAsync(userPrincipal, otherVendor.Id, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_AsUser_ShouldThrowForbidden()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new VendorsService(dbContext);
        var userPrincipal = CreatePrincipal("User", [Scopes.VendorCreate]);
        var request = new VendorsService.CreateRequest("New Vendor", "Notes");

        // Act
        var action = async () => await service.CreateAsync(userPrincipal, request, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ForbiddenAccessException>();
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
