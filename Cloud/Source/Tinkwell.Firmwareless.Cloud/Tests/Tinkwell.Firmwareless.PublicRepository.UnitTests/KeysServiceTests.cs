using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Tinkwell.Firmwareless.Exceptions;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Configuration;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Services;

namespace Tinkwell.Firmwareless.PublicRepository.UnitTests;

public class KeysServiceTests
{
    private readonly ApiKeyOptions _apiKeyOptions = new() { HmacSecret = "test-secret" };

    [Fact]
    public async Task CreateAsync_AsAdmin_ShouldSucceed()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new KeysService(dbContext, Options.Create(_apiKeyOptions));
        var adminPrincipal = CreatePrincipal("Admin", Scopes.All());
        var vendor = new Vendor { Id = Guid.NewGuid(), Name = "Test Vendor" };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();

        var request = new KeysService.CreateRequest(vendor.Id, "Test Key", "User", 30, [Scopes.KeyRead]);

        // Act
        var result = await service.CreateAsync(adminPrincipal, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Key");
        result.VendorId.Should().Be(vendor.Id);
        result.Text.Should().NotBeNullOrEmpty();
        result.Text.Should().StartWith(_apiKeyOptions.KeyPrefix);
    }

    [Fact]
    public async Task CreateAsync_AsUser_ForOwnVendor_ShouldSucceed()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new KeysService(dbContext, Options.Create(_apiKeyOptions));
        var vendor = new Vendor { Id = Guid.NewGuid(), Name = "Test Vendor" };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        var userPrincipal = CreatePrincipal("User", [Scopes.KeyCreate], vendor.Id);

        var request = new KeysService.CreateRequest(vendor.Id, "Test Key", "User", 30, [Scopes.KeyRead]);

        // Act
        var result = await service.CreateAsync(userPrincipal, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Key");
        result.VendorId.Should().Be(vendor.Id);
    }

    [Fact]
    public async Task CreateAsync_AsUser_ForAnotherVendor_ShouldFail()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new KeysService(dbContext, Options.Create(_apiKeyOptions));
        var myVendor = new Vendor { Id = Guid.NewGuid(), Name = "My Vendor" };
        var otherVendor = new Vendor { Id = Guid.NewGuid(), Name = "Other Vendor" };
        dbContext.Vendors.AddRange(myVendor, otherVendor);
        await dbContext.SaveChangesAsync();
        var userPrincipal = CreatePrincipal("User", [Scopes.KeyCreate], myVendor.Id);

        var request = new KeysService.CreateRequest(otherVendor.Id, "Test Key", "User", 30, [Scopes.KeyRead]);

        // Act
        var action = async () => await service.CreateAsync(userPrincipal, request, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task CreateAsync_WithoutScope_ShouldFail()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new KeysService(dbContext, Options.Create(_apiKeyOptions));
        var vendor = new Vendor { Id = Guid.NewGuid(), Name = "Test Vendor" };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        var userPrincipal = CreatePrincipal("User", ["some.other.scope"], vendor.Id);

        var request = new KeysService.CreateRequest(vendor.Id, "Test Key", "User", 30, [Scopes.KeyRead]);

        // Act
        var action = async () => await service.CreateAsync(userPrincipal, request, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task FindAsync_AsUser_ForOwnVendorKey_ShouldSucceed()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new KeysService(dbContext, Options.Create(_apiKeyOptions));
        var vendor = new Vendor { Id = Guid.NewGuid(), Name = "Test Vendor" };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        var adminPrincipal = CreatePrincipal("Admin", Scopes.All());
        var createRequest = new KeysService.CreateRequest(vendor.Id, "Test Key", "User", 30, [Scopes.KeyRead]);
        var createdKey = await service.CreateAsync(adminPrincipal, createRequest, CancellationToken.None);

        var userPrincipal = CreatePrincipal("User", [Scopes.KeyRead], vendor.Id);

        // Act
        var result = await service.FindAsync(userPrincipal, createdKey.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(createdKey.Id);
    }

    [Fact]
    public async Task FindAsync_AsUser_ForOtherVendorKey_ShouldFail()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var service = new KeysService(dbContext, Options.Create(_apiKeyOptions));
        var myVendor = new Vendor { Id = Guid.NewGuid(), Name = "My Vendor" };
        var otherVendor = new Vendor { Id = Guid.NewGuid(), Name = "Other Vendor" };
        dbContext.Vendors.AddRange(myVendor, otherVendor);
        await dbContext.SaveChangesAsync();
        var adminPrincipal = CreatePrincipal("Admin", Scopes.All());
        var createRequest = new KeysService.CreateRequest(otherVendor.Id, "Other Key", "User", 30, [Scopes.KeyRead]);
        var createdKey = await service.CreateAsync(adminPrincipal, createRequest, CancellationToken.None);

        var userPrincipal = CreatePrincipal("User", [Scopes.KeyRead], myVendor.Id);

        // Act
        var action = async () => await service.FindAsync(userPrincipal, createdKey.Id, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    private ClaimsPrincipal CreatePrincipal(string role, string[] scopes, Guid? vendorId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role)
        };

        claims.AddRange(scopes.Select(s => new Claim("scope", s)));

        if (vendorId.HasValue)
        {
            claims.Add(new Claim(CustomClaimTypes.VendorId, vendorId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }
}
