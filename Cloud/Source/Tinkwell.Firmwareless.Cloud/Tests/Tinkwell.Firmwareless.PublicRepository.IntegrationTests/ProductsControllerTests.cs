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

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public ProductsControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostProduct_AsUserWithScope_ForOwnVendor_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId) = await CreateUserKeyAndVendor(scopes: [Scopes.ProductCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);
        var request = new ProductsService.CreateRequest(vendorId, "Test Product", "T-1000", ProductStatus.Production);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductsService.View>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Name.Should().Be("Test Product");
        result?.VendorId.Should().Be(vendorId);
    }

    [Fact]
    public async Task PostProduct_AsUserWithScope_ForAnotherVendor_ShouldBeForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, myVendorId) = await CreateUserKeyAndVendor(scopes: [Scopes.ProductCreate]);
        var (_, otherVendorId) = await CreateAdminKeyAndVendor("OtherVendor"); // Just to get another vendorId
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);
        var request = new ProductsService.CreateRequest(otherVendorId, "Test Product", "T-1000", ProductStatus.Production);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetProducts_AsUser_ShouldReturnOnlyOwnProducts()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, myVendorId) = await CreateUserKeyAndVendor(scopes: [Scopes.ProductRead, Scopes.ProductCreate]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Create a product for our vendor
        var myRequest = new ProductsService.CreateRequest(myVendorId, "My Product", "P-1", ProductStatus.Production);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/products", myRequest);
        createdResponse.EnsureSuccessStatusCode();

        // Create a product for another vendor
        var (adminKey, otherVendorId) = await CreateAdminKeyAndVendor("OtherVendor");
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var otherRequest = new ProductsService.CreateRequest(otherVendorId, "Other Product", "P-2", ProductStatus.Production);
        var otherCreatedResponse = await adminClient.PostAsJsonAsync("/api/v1/products", otherRequest, JsonDefaults.Options);
        otherCreatedResponse.EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/api/v1/products");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<ProductsService.View>>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Items.Should().HaveCount(1);
        result?.Items.Single().VendorId.Should().Be(myVendorId);
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
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync("/api/v1/products");

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<ProductsService.View>>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Find_HappyPath_ShouldReturnOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, vendorId, productId) = await CreateUserKeyVendorAndProduct(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync($"/api/v1/products/{productId}");

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProductsService.View>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Id.Should().Be(productId);
    }

    [Fact]
    public async Task Delete_Product_AsAdmin_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);

        // Create a product to delete
        var createRequest = new ProductsService.CreateRequest(vendorId, "Product to Delete", "Model X", ProductStatus.Production);
        var createResponse = await client.PostAsJsonAsync("/api/v1/products", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createdProduct = await createResponse.Content.ReadFromJsonAsync<ProductsService.View>(JsonDefaults.Options);

        // Act
        var response = await client.DeleteAsync($"/api/v1/products/{createdProduct!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deleted
        var getResponse = await client.GetAsync($"/api/v1/products/{createdProduct.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ProductName_AsAdmin_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);

        // Create a product to update
        var createRequest = new ProductsService.CreateRequest(vendorId, "Original Name", "Original Model", ProductStatus.Production);
        var createResponse = await client.PostAsJsonAsync("/api/v1/products", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createdProduct = await createResponse.Content.ReadFromJsonAsync<ProductsService.View>(JsonDefaults.Options);

        var updateRequest = new ProductsService.UpdateRequest(createdProduct!.Id, "Updated Name", null, null);

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/products", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedProduct = await response.Content.ReadFromJsonAsync<ProductsService.View>(JsonDefaults.Options);
        updatedProduct.Should().NotBeNull();
        updatedProduct?.Name.Should().Be("Updated Name");
    }

    private async Task<(string key, Guid vendorId, Guid productId)> CreateUserKeyVendorAndProduct(string vendorName = "TestVendor", string[]? scopes = null)
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vendor = new Vendor { Id = Guid.NewGuid(), Name = vendorName };
        var product = new Product { Id = Guid.NewGuid(), Name = "Test Product", Model = "T-1000", Vendor = vendor };
        dbContext.Vendors.Add(vendor);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var request = new KeysService.CreateRequest(vendor.Id, "Test User Key", "User", 1, scopes ?? ["test.scope"]);
        var result = await keyService.CreateAsync(CreatePrincipal("Admin", Scopes.All()), request, CancellationToken.None);
        return (result.Text!, vendor.Id, product.Id);
    }

    private async Task<(string key, Guid vendorId, List<Product> products)> SeedProducts(string vendorName = "TestVendor", string[]? scopes = null)
    {
        using var scope = _factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vendor = new Vendor { Id = Guid.NewGuid(), Name = vendorName };
        dbContext.Vendors.Add(vendor);

        var products = new List<Product>
        {
            new Product { Id = Guid.NewGuid(), Name = "Apple", Model = "iPhone", Status = ProductStatus.Production, Vendor = vendor, CreatedAt = DateTimeOffset.UtcNow.AddDays(-5) },
            new Product { Id = Guid.NewGuid(), Name = "Banana", Model = "Galaxy", Status = ProductStatus.Development, Vendor = vendor, CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) },
            new Product { Id = Guid.NewGuid(), Name = "Cherry", Model = "Pixel", Status = ProductStatus.Production, Vendor = vendor, CreatedAt = DateTimeOffset.UtcNow.AddDays(-8) },
            new Product { Id = Guid.NewGuid(), Name = "Date", Model = "iPhone", Status = ProductStatus.Retired, Vendor = vendor, CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) }
        };
        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync();

        var request = new KeysService.CreateRequest(vendor.Id, "Test User Key", "User", 1, scopes ?? ["test.scope"]);
        var result = await keyService.CreateAsync(CreatePrincipal("Admin", Scopes.All()), request, CancellationToken.None);
        return (result.Text!, vendor.Id, products);
    }

    [Fact]
    public async Task FindAll_FilterByNameExact_ShouldReturnFilteredProducts()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, _, products) = await SeedProducts(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync($"/api/v1/products?filter=Name=={products[0].Name}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<ProductsService.View>>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Items.Should().HaveCount(1);
        result?.Items.Single().Name.Should().Be(products[0].Name);
    }

    [Fact]
    public async Task FindAll_FilterByNameContains_ShouldReturnFilteredProducts()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, _, products) = await SeedProducts(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync($"/api/v1/products?filter=Name~{products[0].Name[..3].ToLower()}"); // Case-insensitive

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<ProductsService.View>>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Items.Should().HaveCount(1);
        result?.Items.Single().Name.Should().Be(products[0].Name);
    }

    [Fact]
    public async Task FindAll_FilterByStatus_ShouldReturnFilteredProducts()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, _, products) = await SeedProducts(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync($"/api/v1/products?filter=Status=={ProductStatus.Production}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<ProductsService.View>>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Items.Should().HaveCount(2);
        result?.Items.Should().Contain(p => p.Name == "Apple");
        result?.Items.Should().Contain(p => p.Name == "Cherry");
    }

    [Fact]
    public async Task FindAll_SortByNameAscending_ShouldReturnSortedProducts()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, _, products) = await SeedProducts(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync("/api/v1/products?sort=Name");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<ProductsService.View>>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Items.Should().HaveCount(4);
        result?.Items.Select(p => p.Name).Should().ContainInOrder("Apple", "Banana", "Cherry", "Date");
    }

    [Fact]
    public async Task FindAll_SortByNameDescending_ShouldReturnSortedProducts()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, _, products) = await SeedProducts(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync("/api/v1/products?sort=-Name");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<ProductsService.View>>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Items.Should().HaveCount(4);
        result?.Items.Select(p => p.Name).Should().ContainInOrder("Date", "Cherry", "Banana", "Apple");
    }

    [Fact]
    public async Task FindAll_SortByMultipleColumns_ShouldReturnSortedProducts()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, _, products) = await SeedProducts(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync("/api/v1/products?sort=Model,-CreatedAt");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Services.Queries.FindResponse<ProductsService.View>>(JsonDefaults.Options);
        result.Should().NotBeNull();
        result?.Items.Should().HaveCount(4);
        // Expected order: Galaxy (Banana), iPhone (Date), iPhone (Apple), Pixel (Cherry)
        // Sorted by Model ASC, then CreatedAt DESC
        result?.Items.Select(p => p.Name).Should().ContainInOrder("Banana", "Date", "Apple", "Cherry");
    }

    [Fact]
    public async Task FindAll_WithInvalidFilterSyntax_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, _, _) = await SeedProducts(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync("/api/v1/products?filter=Name:="); // Wrong operator

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Invalid filter term");
    }

    [Fact]
    public async Task FindAll_WithInvalidSortColumn_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (userKey, _, _) = await SeedProducts(scopes: [Scopes.ProductRead]);
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, userKey);

        // Act
        var response = await client.GetAsync("/api/v1/products?sort=NonExistentColumn");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("No property 'NonExistentColumn' found");
    }

    [Fact]
    public async Task Create_WithEmptyName_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var request = new ProductsService.CreateRequest(vendorId, "", "Model1", ProductStatus.Production);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Name");
        error?.ParameterName.Should().Be("Name");
    }

    [Fact]
    public async Task Create_WithEmptyModel_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, vendorId) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var request = new ProductsService.CreateRequest(vendorId, "Name1", "", ProductStatus.Production);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error?.Message.Should().Contain("Model");
        error?.ParameterName.Should().Be("Model");
    }

    [Fact]
    public async Task Create_WithNonExistentVendorId_ShouldReturnNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (adminKey, _) = await CreateAdminKeyAndVendor();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, adminKey);
        var request = new ProductsService.CreateRequest(Guid.NewGuid(), "Name1", "Model1", ProductStatus.Production);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
    }
}
