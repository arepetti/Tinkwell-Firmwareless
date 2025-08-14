using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Configuration;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Repositories;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

public sealed class FirmwaresService(AppDbContext db, BlobContainerClient blob, IOptions<FileUploadOptions> uploadOpts) : ServiceBase(db)
{
    public sealed record CreateRequest(Guid ProductId, string Version, string Compatibility, string Author, string Copyright, string ReleaseNotesUrl, FirmwareType Type, FirmwareStatus Status, IFormFile File);

    public sealed record UpdateRequest(Guid Id, FirmwareStatus? Status);

    public sealed record ResolveBlobNameRequest(Guid VendorId, Guid ProductId, FirmwareType Type, string HardwareVersion);

    public sealed record View(Guid VendorId, Guid ProductId, Guid Id, string Version, string Compatibility, FirmwareType Type, FirmwareStatus Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
   
    public async Task<View> CreateAsync(ClaimsPrincipal user, CreateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        Debug.Assert(request is not null);

        if (request.File.Length > _uploadOpts.MaxFirmwareSizeBytes)
            throw new ArgumentException($"Firmware size cannot exceed {_uploadOpts.MaxFirmwareSizeBytes / 1024 / 1024} MB.", nameof(request.File));

        if (_uploadOpts.AllowedContentTypes.Length > 0 && !_uploadOpts.AllowedContentTypes.Contains(request.File.ContentType))
            throw new ArgumentException($"Invalid content type: {request.File.ContentType}.", nameof(request.File));

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();
        if (!scopes.Contains(Scopes.FirmwareCreate))
            throw new ForbiddenAccessException();

        if (!FirmwareVersion.Validate(request.Version))
            throw new ArgumentException("Invalid firmware version format.", nameof(request.Version));

        if (request.Status == FirmwareStatus.Deprecated)
            throw new ArgumentException("Cannot create a new and already deprecated firmware.", nameof(request.Status));

        var product = await _db.Products.FindAsync([request.ProductId], cancellationToken);
        if (product is null)
            throw new NotFoundException(request.ProductId.ToString(), nameof(request.ProductId));

        if (role != UserRole.Admin && product.VendorId != vendorId)
            throw new ForbiddenAccessException();

        var currentFirmware = await FindApplicableFirmwareAsync(request.ProductId, request.Type, cancellationToken);
        if (currentFirmware is not null)
        {
            var publishingNewerVersion = new FirmwareVersion.VersionStringComparer()
                .Compare(currentFirmware.Version, request.Version) > 0;

            if (!publishingNewerVersion && currentFirmware.Compatibility.Equals(request.Compatibility, StringComparison.InvariantCulture))
            {
                throw new ArgumentException(
                    $"A firmware with version '{currentFirmware.Version}' already exists for this product and type. " +
                    "You can only create a new firmware with a newer version or with different compatibility requirements.",
                    nameof(request.Version));
            }
        }

        var entity = new Firmware
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            Version = request.Version,
            Compatibility = request.Compatibility,
            Type = request.Type,
            Status = request.Status,
            Author = request.Author,
            Copyright = request.Copyright,
            ReleaseNotesUrl = request.ReleaseNotesUrl,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Product = product,
        };

        await _db.Firmwares.AddAsync(entity, cancellationToken);
        await SaveChangesAsync(cancellationToken);

        try
        {
            using var stream = request.File.OpenReadStream();
            await UploadAsync(entity, stream, cancellationToken);
        }
        catch
        {
            _db.Firmwares.Remove(entity);
            await SaveChangesAsync(cancellationToken);
            throw;
        }

        return EntityToView(entity);
    }

    public async Task<FindResponse<View>> FindAllAsync(ClaimsPrincipal user, FindRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        Debug.Assert(request is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();

        IQueryable<Firmware> query = _db.Firmwares
            .AsNoTracking()
            .Include(x => x.Product).ThenInclude(x => x.Vendor);

        if (role != UserRole.Admin)
            query = query.Where(x => x.Product.VendorId == vendorId);

        return await FindAllAsync(
            user,
            Scopes.FirmwareRead,
            query,
            request,
            EntityToView,
            cancellationToken);
    }

    public async Task<View> FindAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();

        if (role == UserRole.None)
            throw new UnauthorizedAccessException("User must be authenticated to use this resource.");

        if (!scopes.Contains(Scopes.FirmwareRead))
            throw new ForbiddenAccessException();

        var entity = await TryFindAsync(role, vendorId, id, cancellationToken);
        if (entity is null)
            throw new NotFoundException(id.ToString(), nameof(id));

        return EntityToView(entity);
    }

    public async Task<View> UpdateAsync(ClaimsPrincipal user, UpdateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        Debug.Assert(request is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();
        if (!scopes.Contains(Scopes.FirmwareUpdate))
            throw new ForbiddenAccessException();

        var entity = await TryFindAsync(role, vendorId, request.Id, cancellationToken);
        if (entity is null)
            throw new NotFoundException(request.Id.ToString(), nameof(request.Id));

        if (request.Status is not null)
            entity.Status = request.Status.Value;

        await SaveChangesAsync(cancellationToken);
        return EntityToView(entity);
    }

    public async Task DeleteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();
        if (!scopes.Contains(Scopes.FirmwareDelete) || role != UserRole.Admin)
            throw new ForbiddenAccessException();

        var entity = await TryFindAsync(role, vendorId, id, cancellationToken);
        if (entity is null)
            throw new NotFoundException(id.ToString(), nameof(id));

        // Leave this in place in case we relax the rules in future and we allow firmwares
        // to be deleted by vendors.
        if (entity.Product.VendorId != vendorId && role != UserRole.Admin)
            throw new ForbiddenAccessException("User is not allowed to delete firmwares for this vendor.");

        _db.Firmwares.Remove(entity);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetBlobName(ClaimsPrincipal user, ResolveBlobNameRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);

        var (role, scopes, _) = user.GetScopesAndVendorId();
        if (role == UserRole.None)
            throw new UnauthorizedAccessException("User must be authenticated to use this resource.");

        if (!scopes.Contains(Scopes.FirmwareDownloadAll))
            throw new ForbiddenAccessException();

        var firmware = await FindApplicableFirmwareAsync(request.ProductId, request.Type, cancellationToken);

        if (firmware is null)
            throw new NotFoundException($"No applicable firmware found for product {request.ProductId} and type {request.Type}.");

        if (firmware.Product.VendorId != request.VendorId)
            throw new NotFoundException($"No applicable firmware found for product {request.ProductId} and vendor {request.VendorId}.");

        return GetBlobName(firmware);
    }

    private readonly AppDbContext _db = db;
    private readonly BlobContainerClient _blob = blob;
    private readonly FileUploadOptions _uploadOpts = uploadOpts.Value;

    private static View EntityToView(Firmware entity)
    {
        return new View(
            entity.Product.VendorId,
            entity.ProductId,
            entity.Id,
            entity.Version,
            entity.Compatibility,
            entity.Type,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private async Task<Firmware?> TryFindAsync(UserRole role, Guid? userVendorId, Guid id, CancellationToken cancellationToken)
    {
        var firmware = await _db.Firmwares
            .Include(x => x.Product).ThenInclude(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (firmware is not null && role != UserRole.Admin && userVendorId != firmware.Product.VendorId)
            return null;

        return firmware;
    }

    private async Task<Firmware?> FindApplicableFirmwareAsync(Guid productId, FirmwareType type, CancellationToken cancellationToken)
    {
        var firmwares = await _db.Firmwares
            .Include(x => x.Product).ThenInclude(p => p.Vendor)
            .Where(x => x.ProductId == productId && x.Type == type && x.Status == FirmwareStatus.Release)
            .ToListAsync(cancellationToken);

        return firmwares
            .OrderByDescending(x => x.Version, new FirmwareVersion.VersionStringComparer())
            .FirstOrDefault();
    }

    private async Task<string> UploadAsync(Firmware firmware, Stream stream, CancellationToken cancellationToken)
    {
        var blobClient = _blob.GetBlobClient(GetBlobName(firmware));

        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
        return blobClient.Uri.ToString();
    }

    private string GetBlobName(Firmware firmware)
        => $"{firmware.Type.ToString().ToLower()}--{firmware.ProductId}--{firmware.Version}.bin";
}
