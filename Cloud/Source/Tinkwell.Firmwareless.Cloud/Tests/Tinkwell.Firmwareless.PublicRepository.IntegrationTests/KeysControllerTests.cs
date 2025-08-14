using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Controllers;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Repositories;

namespace Tinkwell.Firmwareless.PublicRepository.IntegrationTests;

public class KeysControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public KeysControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_WithAdminKey_ShouldReturnCreated()
    {
        // Arrange
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        _client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var request = new KeysService.CreateRequest(vendorId, "New User Key", "User", 7, [Scopes.KeyRead]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<KeysService.View>();
        result.Should().NotBeNull();
        result?.Name.Should().Be("New User Key");
    }

    [Fact]
    public async Task Create_WithNoAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new KeysService.CreateRequest(Guid.NewGuid(), "New User Key", "User", 7, [Scopes.KeyRead]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithNoScope_ShouldReturnForbidden()
    {
        // Arrange
        var (userKey, vendorId, _) = await CreateUserKeyAndVendor(scopes: ["some.other.scope"]);
        _client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);
        var request = new KeysService.CreateRequest(vendorId, "New User Key", "User", 7, [Scopes.KeyRead]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_AsUser_ForAnotherVendorsKey_ShouldReturnNotFound()
    {
        // Arrange
        var (adminKey, otherVendorId) = await CreateAdminKeyAndVendor("OtherVendor");
        var (myUserKey, myVendorId, _) = await CreateUserKeyAndVendor(scopes: [Scopes.KeyRead]);

        // Create a key for the other vendor using the admin key
        var tempClient = _factory.CreateClient();
        tempClient.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var createRequest = new KeysService.CreateRequest(otherVendorId, "Other Key", "User", 1, []);
        var createResponse = await tempClient.PostAsJsonAsync("/api/v1/keys", createRequest);
        var otherKey = await createResponse.Content.ReadFromJsonAsync<KeysService.View>();

        // Use our user key to try and access the other key
        _client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, myUserKey);

        // Act
        var response = await _client.GetAsync($"/api/v1/keys/{otherKey!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    private async Task<(string key, Guid vendorId, Guid id)> CreateUserKeyAndVendor(string vendorName = "TestVendor", string[]? scopes = null)
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vendor = new Vendor { Id = Guid.NewGuid(), Name = vendorName };
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync();

        var request = new KeysService.CreateRequest(vendor.Id, "Test User Key", "User", 1, scopes ?? [Scopes.KeyCreate, Scopes.KeyRead]);
        var result = await keyService.CreateAsync(CreatePrincipal("Admin", Scopes.All()), request, CancellationToken.None);
        return (result.Text!, vendor.Id, result.Id);
    }

    private ClaimsPrincipal CreatePrincipal(string role, string[] scopes, Guid? vendorId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        claims.AddRange(scopes.Select(s => new Claim("scope", s)));
        if (vendorId.HasValue) { claims.Add(new Claim(CustomClaimTypes.VendorId, vendorId.Value.ToString())); }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    // Helper to create a revoked key
    private async Task<string> CreateRevokedKey(Guid vendorId)
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var request = new KeysService.CreateRequest(vendorId, "Revoked Key", "User", 1, [Scopes.KeyRead]);
        var result = await keyService.CreateAsync(CreatePrincipal("Admin", Scopes.All()), request, CancellationToken.None);

        var key = await dbContext.ApiKeys.FindAsync(result.Id);
        key!.RevokedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

        return result.Text!;
    }

    // Helper to create an expired key
    private async Task<string> CreateExpiredKey(Guid vendorId)
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create a key with 1 day validity, then set its expiration to the past directly in the DB
        var request = new KeysService.CreateRequest(vendorId, "Expired Key", "User", 1, [Scopes.KeyRead]);
        var result = await keyService.CreateAsync(CreatePrincipal("Admin", Scopes.All()), request, CancellationToken.None);

        var key = await dbContext.ApiKeys.FindAsync(result.Id);
        key!.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1); // Set expiration to yesterday
        await dbContext.SaveChangesAsync();

        return result.Text!;
    }

    [Fact]
    public async Task GetKeys_WithWrongPrefix_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, "wrong_prefix_ak_somevalidkey");

        // Act
        var response = await client.GetAsync("/api/v1/keys");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetKeys_WithInvalidBase64_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, "ak_invalid-base64-!");

        // Act
        var response = await client.GetAsync("/api/v1/keys");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetKeys_WithInvalidHmac_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        // Generate a valid key, then tamper with its HMAC part
        var (validKey, vendorId, _) = await CreateUserKeyAndVendor(scopes: [Scopes.KeyRead]);
        var parts = validKey.Split('_');
        var tamperedKey = parts[0] + "_" + parts[1][..^2] + "AA"; // Change last two chars of Base64 payload
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, tamperedKey);

        // Act
        var response = await client.GetAsync("/api/v1/keys");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetKeys_WithRevokedKey_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor(); // Need a vendor to create a key
        var revokedKey = await CreateRevokedKey(vendorId);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, revokedKey);

        // Act
        var response = await client.GetAsync("/api/v1/keys");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetKeys_WithExpiredKey_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor(); // Need a vendor to create a key
        var expiredKey = await CreateExpiredKey(vendorId);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, expiredKey);

        // Act
        var response = await client.GetAsync("/api/v1/keys");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FindAll_HappyPath_ShouldReturnOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        // Ensure at least one key exists
        await CreateUserKeyAndVendor("Test vendor");

        // Act
        var response = await client.GetAsync("/api/v1/keys");

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<KeysService.View>>();
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
        // Create a specific key to find
        var (userKey, _, id) = await CreateUserKeyAndVendor("Test vendor");
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        var keyView = await keyService.FindAsync(CreatePrincipal("Admin", Scopes.All()), id, CancellationToken.None);

        // Act
        var response = await client.GetAsync($"/api/v1/keys/{keyView.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<KeysService.View>();
        result.Should().NotBeNull();
        result?.Id.Should().Be(keyView.Id);
    }

    [Fact]
    public async Task Create_WithEmptyName_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var request = new KeysService.CreateRequest(vendorId, "", "User", 30, [Scopes.KeyRead]);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Name is required.");
        error?.ParameterName.Should().Be("Name");
    }

    [Fact]
    public async Task Create_WithInvalidRole_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var request = new KeysService.CreateRequest(vendorId, "Valid Name", "InvalidRole", 30, [Scopes.KeyRead]);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Role must be User or Admin.");
        error?.ParameterName.Should().Be("Role");
    }

    [Fact]
    public async Task Create_WithDaysValidTooLow_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var request = new KeysService.CreateRequest(vendorId, "Valid Name", "User", 0, [Scopes.KeyRead]);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Validity must be between 1 and 365 days.");
        error?.ParameterName.Should().Be("DaysValid");
    }

    [Fact]
    public async Task Create_WithDaysValidTooHigh_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var request = new KeysService.CreateRequest(vendorId, "Valid Name", "User", 366, [Scopes.KeyRead]);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Validity must be between 1 and 365 days.");
        error?.ParameterName.Should().Be("DaysValid");
    }
}
