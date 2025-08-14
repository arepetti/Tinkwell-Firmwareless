using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Tinkwell.Firmwareless.PublicRepository.IntegrationTests;

public class VendorsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public VendorsControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostVendor_WithAdminKey_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, _) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var request = new VendorsService.CreateRequest("New Vendor", "Notes");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/vendors", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<VendorsService.View>();
        result.Should().NotBeNull();
        result?.Name.Should().Be("New Vendor");
    }

    [Fact]
    public async Task PostVendor_WithUserKey_ShouldBeForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, _) = await CreateUserKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);
        var request = new VendorsService.CreateRequest("New Vendor", "Notes");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/vendors", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetVendor_AsUser_ForOwnVendor_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId) = await CreateUserKeyAndVendor(scopes: [Scopes.VendorRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync($"/api/v1/vendors/{vendorId}");

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VendorsService.View>();
        result.Should().NotBeNull();
        result?.Id.Should().Be(vendorId);
    }

    private async Task<(string key, Guid vendorId)> CreateAdminKeyAndVendor(string vendorName = "TestVendor")
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vendor = new Vendor { Id = Guid.NewGuid(), Name = vendorName };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();

        var key = await keyService.UnsafeCreateForAdminAsync();
        return (key, vendor.Id);
    }

    private async Task<(string key, Guid vendorId)> CreateUserKeyAndVendor(string vendorName = "TestVendor", string[]? scopes = null)
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vendor = new Vendor { Id = Guid.NewGuid(), Name = vendorName };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();

        var request = new KeysService.CreateRequest(vendor.Id, "Test User Key", "User", 1, scopes ?? ["test.scope"]);
        var result = await keyService.CreateAsync(CreatePrincipal("Admin", Scopes.All()), request, CancellationToken.None);
        return (result.Text!, vendor.Id);
    }

    private ClaimsPrincipal CreatePrincipal(string role, string[] scopes, Guid? vendorId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        claims.AddRange(scopes.Select(s => new Claim("scope", s)));
        if (vendorId.HasValue) { claims.Add(new Claim(CustomClaimTypes.VendorId, vendorId.Value.ToString())); }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task FindAll_HappyPath_ShouldReturnOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);

        // Act
        var response = await client.GetAsync("/api/v1/vendors");

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<VendorsService.View>>();
        result.Should().NotBeNull();
        result?.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Find_HappyPath_ShouldReturnOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);

        // Act
        var response = await client.GetAsync($"/api/v1/vendors/{vendorId}");

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VendorsService.View>();
        result.Should().NotBeNull();
        result?.Id.Should().Be(vendorId);
    }
}
