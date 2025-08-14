using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Text;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Configuration;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Repositories;
using Tinkwell.Firmwareless.PublicRepository.UnitTests.Fakes;

namespace Tinkwell.Firmwareless.PublicRepository.UnitTests;

public class FirmwaresServiceTests
{
    private readonly IOptions<FileUploadOptions> _fileUploadOptions = Options.Create(new FileUploadOptions());

    [Fact]
    public async Task CreateAsync_WhenUploadFails_ShouldRollbackDatabaseChanges()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var fakeBlobClient = new FakeBlobContainerClient { ShouldThrowOnUpload = true };
        var service = new FirmwaresService(dbContext, fakeBlobClient, _fileUploadOptions);

        var vendor = new Vendor { Id = Guid.NewGuid(), Name = "Test Vendor" };
        var product = new Product { Id = Guid.NewGuid(), Name = "Test Product", Model = "T-1000", Vendor = vendor };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var userPrincipal = CreatePrincipal("User", [Scopes.FirmwareCreate], vendor.Id);
        var formFile = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("firmware content")), 0, 100, "firmware", "firmware.bin");
        var request = new FirmwaresService.CreateRequest(product.Id, "1.0.0", "esp32", "author", "copyright", "notes.url", FirmwareType.Firmlet, FirmwareStatus.Release, formFile);

        // Act
        var action = async () => await service.CreateAsync(userPrincipal, request, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<Exception>().WithMessage("Simulated upload failure.");
        dbContext.Firmwares.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithInvalidVersionString_ShouldThrowArgumentException()
    {
        // Arrange
        var dbContext = DbContextHelper.GetInMemoryDbContext();
        var fakeBlobClient = new FakeBlobContainerClient();
        var service = new FirmwaresService(dbContext, fakeBlobClient, _fileUploadOptions);
        var userPrincipal = CreatePrincipal("User", [Scopes.FirmwareCreate], Guid.NewGuid());
        var formFile = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("firmware content")), 0, 100, "firmware", "firmware.bin");
        var request = new FirmwaresService.CreateRequest(Guid.NewGuid(), "1.0.0/invalid", "esp32", "author", "copyright", "notes.url", FirmwareType.Firmlet, FirmwareStatus.Release, formFile);

        // Act
        var action = async () => await service.CreateAsync(userPrincipal, request, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
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
