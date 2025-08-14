using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Configuration;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Repositories;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

public sealed class KeysService : ServiceBase
{
    public KeysService(AppDbContext db, IOptions<ApiKeyOptions> opts) : base(db)
    {
        _db = db;
        _opts = opts.Value;
    }

    public sealed record CreateRequest(Guid? VendorId, string Name, string Role, int DaysValid, string[] Scopes);

    public sealed record CreatePublisherRequest(Guid? VendorId);

    public sealed record View(Guid Id, Guid? VendorId, string Name, string Role, string[] Scopes, string? Text, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt);

    public async Task<View> CreateAsync(ClaimsPrincipal user, CreateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        Debug.Assert(request is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();

        if (role == UserRole.None)
            throw new UnauthorizedAccessException("User must be authenticated to create an API key.");

        if (!scopes.Contains(Scopes.KeyCreate))
            throw new ForbiddenAccessException("You do not have the permission to create API keys.");

        if (role == UserRole.User && vendorId != request.VendorId)
            throw new ForbiddenAccessException("User is not authorized to create an API key for this vendor.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request.Name));

        if (request.Role is not ("User" or "Admin"))
            throw new ArgumentException("Role must be User or Admin.", nameof(request.Role));

        if (request.DaysValid < 1 || request.DaysValid > 365)
            throw new ArgumentException("Validity must be between 1 and 365 days.", nameof(request.DaysValid));

        // TODO: add validation for the scopes they want to include!!!
        // For example: a vendor should NEVER have firmware.download_all in their API key and they can't have
        // scopes to delete keys and manage vendors (it'll fail anyway but it shouldn't be possible to create a key
        // with those scopes). Also: users cannot create an admin api key.

        var (result, plaintext) = await UnsafeCreateWithoutValidationAsync(request, vendorSpecific: true, cancellationToken);
        return EntityToView(result, plaintext);
    }

    public async Task<string> UnsafeCreateForAdminAsync(CancellationToken cancellationToken = default)
    {
        var request = new CreateRequest(
            VendorId: null,
            Name: "Bootstrapping Admin Key",
            Role: "Admin",
            DaysValid: 3,
            Scopes: Scopes.All());

        var (_, plaintext) = await UnsafeCreateWithoutValidationAsync(request, vendorSpecific: false, cancellationToken);
        return plaintext;
    }

    public async Task<string> UnsafeCreateForHubAsync(CancellationToken cancellationToken = default)
    {
        var request = new CreateRequest(
            VendorId: null,
            Name: "Generic Hub client",
            Role: "User",
            DaysValid: -1,
            Scopes: [Scopes.FirmwareDownloadAll]);

        var (_, plaintext) = await UnsafeCreateWithoutValidationAsync(request, vendorSpecific: false, cancellationToken);
        return plaintext;
    }

    public Task<bool> HasAdminKeyAsync(CancellationToken cancellationToken = default)
        => _db.ApiKeys.AnyAsync(x => x.Role == "Admin" && x.RevokedAt == null, cancellationToken);

    public async Task<FindResponse<View>> FindAllAsync(ClaimsPrincipal user, FindRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);

        var (role, _, vendorId) = user.GetScopesAndVendorId();
        var query = _db.ApiKeys.AsNoTracking();
        if (role != UserRole.Admin)
            query = query.Where(x => x.VendorId == vendorId);

        return await FindAllAsync(user, Scopes.KeyRead, query, request, EntityToView, cancellationToken);
    }

    public async Task<View> FindAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        
        var (role, scopes, vendorId) = user.GetScopesAndVendorId();

        if (role == UserRole.None)
            throw new UnauthorizedAccessException("User must be authenticated to use API keys.");

        if (!scopes.Contains(Scopes.KeyRead))
            throw new ForbiddenAccessException("You do not have the permission to read API keys.");

        var apiKey = await TryFindAsync(role, vendorId, id, cancellationToken);
        if (apiKey is null)
            throw new NotFoundException(id.ToString(), nameof(id));

        return EntityToView(apiKey);
    }

    public async Task RevokeAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        
        var (role, scopes, vendorId) = user.GetScopesAndVendorId();

        if (role == UserRole.None)
            throw new UnauthorizedAccessException("User must be authenticated to use API keys.");

        if (!scopes.Contains(Scopes.KeyRevoke))
            throw new ForbiddenAccessException("You do not have the permission to revoke API keys.");

        var apiKey = await TryFindAsync(role, vendorId, id, cancellationToken);
        if (apiKey is null)
            throw new NotFoundException(id.ToString(), nameof(id));

        apiKey.RevokedAt = DateTimeOffset.UtcNow;
        await SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        
        var (role, scopes, vendorId) = user.GetScopesAndVendorId();

        if (role == UserRole.None)
            throw new UnauthorizedAccessException("User must be authenticated to use API keys.");

        if (!scopes.Contains(Scopes.KeyDelete))
            throw new ForbiddenAccessException("You do not have the permission to delete API keys.");

        var apiKey = await TryFindAsync(role, vendorId, id, cancellationToken);
        if (apiKey is null)
            throw new NotFoundException(id.ToString(), nameof(id));

        _db.ApiKeys.Remove(apiKey);
        await SaveChangesAsync(cancellationToken);
    }

    private readonly AppDbContext _db;
    private readonly ApiKeyOptions _opts;

    private async Task<(ApiKey Result, string Plaintext)> UnsafeCreateWithoutValidationAsync(CreateRequest request, bool vendorSpecific, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var plaintext = ApiKeyFormat.Generate(id, _opts);
        var (hash, salt) = ApiKeyHasher.Hash(plaintext);

        Vendor? vendor = null;
        if (vendorSpecific)
        {
            vendor = await _db.Vendors.FindAsync([request.VendorId], cancellationToken);
            if (vendor is null)
                throw new NotFoundException(request.VendorId?.ToString() ?? "", nameof(request.VendorId));
        }

        var entity = new ApiKey
        {
            Id = id,
            Name = request.Name,
            Role = request.Role,
            Hash = hash,
            Salt = salt,
            Scopes = Scopes.ToString(request.Scopes),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = request.DaysValid <= 0 ? null : DateTimeOffset.UtcNow.AddDays(request.DaysValid),

            Vendor = vendor
        };

        _db.ApiKeys.Add(entity);
        await SaveChangesAsync(cancellationToken);

        return (entity, plaintext);
    }

    private async Task<ApiKey?> TryFindAsync(UserRole role, Guid? vendorId, Guid id, CancellationToken cancellationToken)
    {
        var apiKey = await _db.ApiKeys.FindAsync([id], cancellationToken);
        if (apiKey is null)
            return null;

        if (role != UserRole.Admin && apiKey.VendorId != vendorId)
            return null;

        return apiKey;
    }

    private View EntityToView(ApiKey entity)
        => EntityToView(entity, $"{_opts.KeyPrefix}*****");

    private static View EntityToView(ApiKey entity, string? plaintext)
    {
        return new View(
            entity.Id,
            entity.VendorId,
            entity.Name,
            entity.Role,
            Scopes.Parse(entity.Scopes),
            plaintext,
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.RevokedAt);
    }
}
