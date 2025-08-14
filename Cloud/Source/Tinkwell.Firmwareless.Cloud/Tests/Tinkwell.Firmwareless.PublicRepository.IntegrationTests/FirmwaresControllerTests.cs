using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Repositories;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Controllers;
using System.Net.Http.Json;

namespace Tinkwell.Firmwareless.PublicRepository.IntegrationTests;

public class FirmwaresControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public FirmwaresControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithValidFile_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(productId.ToString()), "ProductId");
        content.Add(new StringContent("1.0.0"), "Version");
        content.Add(new StringContent("esp32"), "Compatibility");
        content.Add(new StringContent("Test Author"), "Author");
        content.Add(new StringContent("Test Copyright"), "Copyright");
        content.Add(new StringContent("http://notes.url"), "ReleaseNotesUrl");
        content.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        content.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake firmware file"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "File", "firmware.bin");

        // Act
        var response = await client.PostAsync("/api/v1/firmwares", content);

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Download_WithValidRequest_ShouldReturnFileStream()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareDownloadAll, Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Create a firmware to download
        using var setupContent = new MultipartFormDataContent();
        setupContent.Add(new StringContent(productId.ToString()), "ProductId");
        setupContent.Add(new StringContent("1.2.3"), "Version");
        setupContent.Add(new StringContent("esp32"), "Compatibility");
        setupContent.Add(new StringContent("Author"), "Author");
        setupContent.Add(new StringContent("Copyright"), "Copyright");
        setupContent.Add(new StringContent("notes.url"), "ReleaseNotesUrl");
        setupContent.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        setupContent.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var setupFileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("real firmware"));
        setupFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        setupContent.Add(setupFileContent, "File", "firmware.bin");
        var setupResponse = await client.PostAsync("/api/v1/firmwares", setupContent);
        setupResponse.EnsureSuccessStatusCode();

        var request = new FirmwaresController.DownloadRequest(vendorId, productId, FirmwareType.Firmlet, "1.0", "esp32");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/firmwares/download", request);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().Should().Be("application/octet-stream");
        var responseBytes = await response.Content.ReadAsByteArrayAsync();
        responseBytes.Should().Equal(Encoding.UTF8.GetBytes("fake firmware"));
    }

    private async Task<(string key, Guid vendorId, Guid productId)> CreateUserKeyVendorAndProduct(string vendorName = "TestVendor", string[]? scopes = null)
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vendor = new Vendor { Id = Guid.NewGuid(), Name = vendorName };
        var product = new Product { Id = Guid.NewGuid(), Name = "Test Product", Model = "T-1000", Vendor = vendor };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var request = new KeysService.CreateRequest(vendor.Id, "Test User Key", "User", 1, scopes ?? ["test.scope"]);
        var result = await keyService.CreateAsync(CreatePrincipal("Admin", Scopes.All()), request, CancellationToken.None);
        return (result.Text!, vendor.Id, product.Id);
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
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareRead, Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Create a firmware to ensure there's data
        using var setupContent = new MultipartFormDataContent();
        setupContent.Add(new StringContent(productId.ToString()), "ProductId");
        setupContent.Add(new StringContent("1.0.0"), "Version");
        setupContent.Add(new StringContent("esp32"), "Compatibility");
        setupContent.Add(new StringContent("Author"), "Author");
        setupContent.Add(new StringContent("Copyright"), "Copyright");
        setupContent.Add(new StringContent("notes.url"), "ReleaseNotesUrl");
        setupContent.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        setupContent.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var setupFileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("real firmware"));
        setupFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        setupContent.Add(setupFileContent, "File", "firmware.bin");
        var setupResponse = await client.PostAsync("/api/v1/firmwares", setupContent);
        setupResponse.EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/api/v1/firmwares");

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<FirmwaresService.View>>();
        result.Should().NotBeNull();
        result?.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Find_HappyPath_ShouldReturnOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareRead, Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Create a firmware to find
        using var setupContent = new MultipartFormDataContent();
        setupContent.Add(new StringContent(productId.ToString()), "ProductId");
        setupContent.Add(new StringContent("1.0.0"), "Version");
        setupContent.Add(new StringContent("esp32"), "Compatibility");
        setupContent.Add(new StringContent("Author"), "Author");
        setupContent.Add(new StringContent("Copyright"), "Copyright");
        setupContent.Add(new StringContent("notes.url"), "ReleaseNotesUrl");
        setupContent.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        setupContent.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var setupFileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("real firmware"));
        setupFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        setupContent.Add(setupFileContent, "File", "firmware.bin");
        var setupResponse = await client.PostAsync("/api/v1/firmwares", setupContent);
        setupResponse.EnsureSuccessStatusCode();
        var createdFirmware = await setupResponse.Content.ReadFromJsonAsync<FirmwaresService.View>();

        // Act
        var response = await client.GetAsync($"/api/v1/firmwares/{createdFirmware!.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FirmwaresService.View>();
        result.Should().NotBeNull();
        result?.Id.Should().Be(createdFirmware.Id);
    }

    [Fact]
    public async Task Download_NoFirmwareAvailable_ShouldReturnNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareDownloadAll]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Do NOT create any firmware for this product
        var request = new FirmwaresController.DownloadRequest(vendorId, productId, FirmwareType.Firmlet, "1.0", "esp32");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/firmwares/download", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("No applicable firmware found for product");
    }

    [Fact]
    public async Task Delete_Firmware_AsUser_ShouldBeForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareCreate, Scopes.FirmwareRead, Scopes.FirmwareDelete]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Create a firmware to attempt to delete
        using var setupContent = new MultipartFormDataContent();
        setupContent.Add(new StringContent(productId.ToString()), "ProductId");
        setupContent.Add(new StringContent("1.0.0"), "Version");
        setupContent.Add(new StringContent("esp32"), "Compatibility");
        setupContent.Add(new StringContent("Author"), "Author");
        setupContent.Add(new StringContent("Copyright"), "Copyright");
        setupContent.Add(new StringContent("notes.url"), "ReleaseNotesUrl");
        setupContent.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        setupContent.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var setupFileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("real firmware"));
        setupFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        setupContent.Add(setupFileContent, "File", "firmware.bin");
        var setupResponse = await client.PostAsync("/api/v1/firmwares", setupContent);
        setupResponse.EnsureSuccessStatusCode();
        var createdFirmware = await setupResponse.Content.ReadFromJsonAsync<FirmwaresService.View>();

        // Act
        var response = await client.DeleteAsync($"/api/v1/firmwares/{createdFirmware!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Verify it's NOT deleted
        var getResponse = await client.GetAsync($"/api/v1/firmwares/{createdFirmware.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK); // Should still exist
    }

    [Fact]
    public async Task Update_FirmwareStatus_AsAdmin_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareCreate, Scopes.FirmwareUpdate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);

        // Create a firmware to update
        using var setupContent = new MultipartFormDataContent();
        setupContent.Add(new StringContent(productId.ToString()), "ProductId");
        setupContent.Add(new StringContent("1.0.0"), "Version");
        setupContent.Add(new StringContent("esp32"), "Compatibility");
        setupContent.Add(new StringContent("Author"), "Author");
        setupContent.Add(new StringContent("Copyright"), "Copyright");
        setupContent.Add(new StringContent("notes.url"), "ReleaseNotesUrl");
        setupContent.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        setupContent.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var setupFileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("real firmware"));
        setupFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        setupContent.Add(setupFileContent, "File", "firmware.bin");
        var setupResponse = await client.PostAsync("/api/v1/firmwares", setupContent);
        setupResponse.EnsureSuccessStatusCode();
        var createdFirmware = await setupResponse.Content.ReadFromJsonAsync<FirmwaresService.View>();

        var updateRequest = new FirmwaresService.UpdateRequest(createdFirmware!.Id, FirmwareStatus.Deprecated);

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/firmwares", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedFirmware = await response.Content.ReadFromJsonAsync<FirmwaresService.View>();
        updatedFirmware.Should().NotBeNull();
        updatedFirmware?.Status.Should().Be(FirmwareStatus.Deprecated);
    }

    [Fact]
    public async Task Create_WithTooLargeFile_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(productId.ToString()), "ProductId");
        content.Add(new StringContent("1.0.0"), "Version");
        content.Add(new StringContent("esp32"), "Compatibility");
        content.Add(new StringContent("Test Author"), "Author");
        content.Add(new StringContent("Test Copyright"), "Copyright");
        content.Add(new StringContent("http://notes.url"), "ReleaseNotesUrl");
        content.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        content.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var largeFileContent = new ByteArrayContent(new byte[17 * 1024 * 1024]); // 17MB file
        largeFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(largeFileContent, "File", "large_firmware.bin");

        // Act
        var response = await client.PostAsync("/api/v1/firmwares", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Firmware size cannot exceed 16 MB.");
    }

    [Fact]
    public async Task Create_WithInvalidContentType_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(productId.ToString()), "ProductId");
        content.Add(new StringContent("1.0.0"), "Version");
        content.Add(new StringContent("esp32"), "Compatibility");
        content.Add(new StringContent("Test Author"), "Author");
        content.Add(new StringContent("Test Copyright"), "Copyright");
        content.Add(new StringContent("http://notes.url"), "ReleaseNotesUrl");
        content.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        content.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var invalidFileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake firmware file"));
        invalidFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"); // Invalid content type
        content.Add(invalidFileContent, "File", "firmware.json");

        // Act
        var response = await client.PostAsync("/api/v1/firmwares", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Invalid content type: application/json.");
    }

    [Fact]
    public async Task Create_WithInvalidVersionString_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(productId.ToString()), "ProductId");
        content.Add(new StringContent("1.0.0/../invalid"), "Version"); // Invalid version string
        content.Add(new StringContent("esp32"), "Compatibility");
        content.Add(new StringContent("Test Author"), "Author");
        content.Add(new StringContent("Test Copyright"), "Copyright");
        content.Add(new StringContent("http://notes.url"), "ReleaseNotesUrl");
        content.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        content.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake firmware file"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "File", "firmware.bin");

        // Act
        var response = await client.PostAsync("/api/v1/firmwares", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Invalid firmware version format");
    }

    [Fact]
    public async Task Create_WithDeprecatedStatus_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(productId.ToString()), "ProductId");
        content.Add(new StringContent("1.0.0"), "Version");
        content.Add(new StringContent("esp32"), "Compatibility");
        content.Add(new StringContent("Test Author"), "Author");
        content.Add(new StringContent("Test Copyright"), "Copyright");
        content.Add(new StringContent("http://notes.url"), "ReleaseNotesUrl");
        content.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        content.Add(new StringContent(FirmwareStatus.Deprecated.ToString()), "Status"); // Deprecated status
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake firmware file"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "File", "firmware.bin");

        // Act
        var response = await client.PostAsync("/api/v1/firmwares", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Cannot create a new and already deprecated firmware.");
    }

    [Fact]
    public async Task Create_WithNonExistentProductId_ShouldReturnNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(Guid.NewGuid().ToString()), "ProductId"); // Non-existent ProductId
        content.Add(new StringContent("1.0.0"), "Version");
        content.Add(new StringContent("esp32"), "Compatibility");
        content.Add(new StringContent("Test Author"), "Author");
        content.Add(new StringContent("Test Copyright"), "Copyright");
        content.Add(new StringContent("http://notes.url"), "ReleaseNotesUrl");
        content.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        content.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake firmware file"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "File", "firmware.bin");

        // Act
        var response = await client.PostAsync("/api/v1/firmwares", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithConflictingVersionAndCompatibility_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.FirmwareCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // First upload a firmware
        using var initialContent = new MultipartFormDataContent();
        initialContent.Add(new StringContent(productId.ToString()), "ProductId");
        initialContent.Add(new StringContent("1.0.0"), "Version");
        initialContent.Add(new StringContent("esp32"), "Compatibility");
        initialContent.Add(new StringContent("Test Author"), "Author");
        initialContent.Add(new StringContent("Test Copyright"), "Copyright");
        initialContent.Add(new StringContent("http://notes.url"), "ReleaseNotesUrl");
        initialContent.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        initialContent.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var initialFileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("initial firmware"));
        initialFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        initialContent.Add(initialFileContent, "File", "initial_firmware.bin");
        var initialResponse = await client.PostAsync("/api/v1/firmwares", initialContent);
        initialResponse.EnsureSuccessStatusCode();

        // Try to upload another with the same version and compatibility
        using var conflictingContent = new MultipartFormDataContent();
        conflictingContent.Add(new StringContent(productId.ToString()), "ProductId");
        conflictingContent.Add(new StringContent("1.0.0"), "Version");
        conflictingContent.Add(new StringContent("esp32"), "Compatibility");
        conflictingContent.Add(new StringContent("Test Author"), "Author");
        conflictingContent.Add(new StringContent("Test Copyright"), "Copyright");
        conflictingContent.Add(new StringContent("http://notes.url"), "ReleaseNotesUrl");
        conflictingContent.Add(new StringContent(FirmwareType.Firmlet.ToString()), "Type");
        conflictingContent.Add(new StringContent(FirmwareStatus.Release.ToString()), "Status");
        var conflictingFileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("conflicting firmware"));
        conflictingFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        conflictingContent.Add(conflictingFileContent, "File", "conflicting_firmware.bin");

        // Act
        var response = await client.PostAsync("/api/v1/firmwares", conflictingContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("A firmware with version '");
        error?.Message.Should().Contain("already exists for this product and type.");
    }
}
