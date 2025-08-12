using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Repositories;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

public sealed class KeyService
{
    public KeyService(AppDbContext db, IOptions<ApiKeyOptions> opts)
    {
        _db = db;
        _opts = opts.Value;
    }

    public sealed record CreateKeyRequest(Guid? VendorId, string Name, string Role, int DaysValid, string[] Scopes);

    public sealed record KeyView(Guid Id, Guid? VendorId, string Name, string Role, string[] Scopes, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt);

    public async Task<KeyView> CreateAsync(ClaimsPrincipal user, CreateKeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

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

        var (result, _) = await CreateWithoutValidationAsync(request, vendorSpecific: true, cancellationToken);
        return result;
    }

    public async Task<string> UnsafeCreateForAdminAsync(CancellationToken cancellationToken = default)
    {
        var request = new CreateKeyRequest(
            VendorId: null,
            Name: "Bootstrapping Admin Key",
            Role: "Admin",
            DaysValid: -1,
            Scopes: Scopes.All());

        var (_, plaintext) = await CreateWithoutValidationAsync(request, vendorSpecific: false, cancellationToken);
        return plaintext;
    }

    public Task<bool> HasAdminKeyAsync(CancellationToken cancellationToken = default)
        => _db.ApiKeys.AnyAsync(x => x.Role == "Admin" && x.RevokedAt == null, cancellationToken);

    public async Task<FindResponse<KeyView>> FindAllAsync(ClaimsPrincipal user, FindRequest request, CancellationToken cancellationToken)
    {
        var (role, scopes, vendorId) = user.GetScopesAndVendorId();

        if (role == UserRole.None)
            throw new UnauthorizedAccessException("User must be authenticated to list API keys.");

        if (!scopes.Contains(Scopes.KeyRead))
            throw new ForbiddenAccessException("You do not have the permission to read API keys.");

        if (request.PageIndex is not null && request.PageIndex < 0)
            throw new ArgumentException("Page index must be greater than or equal to 0.", nameof(request.PageIndex));

        if (request.PageLength is not null && request.PageLength <= 0)
            throw new ArgumentException("Page length must be greater than 0.", nameof(request.PageIndex));

        if (request.PageLength is not null && request.PageLength > FindRequest.MaximumPageLength)
            throw new ArgumentException($"Page length must be less than {FindRequest.MaximumPageLength}.", nameof(request.PageIndex));

        var query = _db.ApiKeys.AsNoTracking();

        int pageIndex = request.PageIndex ?? 0;
        int pageLength = request.PageLength ?? FindRequest.DefaultPageLength;

        if (role != UserRole.Admin)
            query = query.Where(x => x.VendorId == vendorId);

        if (string.IsNullOrWhiteSpace(request.Sort))
            query = query.ApplySorting(request.Sort);
        else
            query = query.OrderByDescending(x => x.CreatedAt);

        var items = query
            .ApplyFilters(request.Filter)
            .Skip(pageIndex * pageLength)
            .Take(pageLength)
            .Select(EntityToView);

        var total = await query.CountAsync(cancellationToken);
        return new FindResponse<KeyView>(items.ToList(), total, pageIndex, pageLength);
    }

    public async Task<KeyView> FindAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
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

    private async Task<(KeyView Result, string Plaintext)> CreateWithoutValidationAsync(CreateKeyRequest request, bool vendorSpecific, CancellationToken cancellationToken)
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

        return (EntityToView(entity), plaintext);
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

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException();
        }
        catch (DbUpdateException e) when (e.InnerException is ConstraintException)
        {
            throw new ArgumentException(e.Message);
        }
    }

    private static KeyView EntityToView(ApiKey entity)
    {
        return new KeyView(
            entity.Id,
            entity.VendorId,
            entity.Name,
            entity.Role,
            Scopes.Parse(entity.Scopes),
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.RevokedAt);
    }
}

#pragma warning restore CA2208 // Instantiate argument exceptions correctly
