using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Repositories;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

public sealed class VendorService(AppDbContext db) : ServiceBase(db)
{
    public sealed record CreateRequest(string Name, string Notes);

    public sealed record UpdateRequest(Guid Id, string? Name, string? Notes);

    public sealed record VendorView(Guid Id, string Name, string Notes, DateTimeOffset CreatedAt);

    public async Task<VendorView> CreateAsync(ClaimsPrincipal user, CreateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        Debug.Assert(request is not null);

        var (role, scopes, _) = user.GetScopesAndVendorId();
        if (role != UserRole.Admin || !scopes.Contains(Scopes.VendorCreate))
            throw new ForbiddenAccessException();

        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name, nameof(request.Name));

        var entity = new Vendor
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var tracker = await _db.AddAsync(entity, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return EntityToView(entity);
    }

    public async Task<FindResponse<VendorView>> FindAllAsync(ClaimsPrincipal user, FindRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);

        var (role, _, vendorId) = user.GetScopesAndVendorId();
        if (role != UserRole.Admin)
            throw new ForbiddenAccessException();

        return await FindAllAsync(
            user,
            Scopes.VendorRead,
            _db.Vendors.AsNoTracking(),
            request,
            EntityToView,
            cancellationToken);
    }

    public async Task<VendorView> FindAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();

        if (role == UserRole.None)
            throw new UnauthorizedAccessException("User must be authenticated to use this resource.");

        if (!scopes.Contains(Scopes.VendorRead))
            throw new ForbiddenAccessException();

        var entity = await TryFindAsync(role, vendorId, id, cancellationToken);
        if (entity is null)
            throw new NotFoundException(id.ToString(), nameof(id));

        return EntityToView(entity);
    }

    public async Task<VendorView> UpdateAsync(ClaimsPrincipal user, UpdateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        Debug.Assert(request is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();
        if (role != UserRole.Admin || !scopes.Contains(Scopes.VendorUpdate))
            throw new ForbiddenAccessException();

        var entity = await TryFindAsync(role, vendorId, request.Id, cancellationToken);
        if (entity is null)
            throw new NotFoundException(request.Id.ToString(), nameof(request.Id));

        if (request.Name is not null)
            entity.Name = request.Name;

        if (request.Notes is not null)
            entity.Notes = request.Notes;

        await SaveChangesAsync(cancellationToken);
        return EntityToView(entity);
    }

    public async Task DeleteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();
        if (role != UserRole.Admin || !scopes.Contains(Scopes.VendorDelete))
            throw new ForbiddenAccessException();

        var entity = await TryFindAsync(role, vendorId, id, cancellationToken);
        if (entity is null)
            throw new NotFoundException(id.ToString(), nameof(id));

        _db.Vendors.Remove(entity);

        await SaveChangesAsync(cancellationToken);
    }

    private readonly AppDbContext _db = db;

    private static VendorView EntityToView(Vendor entity)
    {
        return new VendorView(
            entity.Id,
            entity.Name,
            entity.Notes,
            entity.CreatedAt);
    }

    private async Task<Vendor?> TryFindAsync(UserRole role, Guid? userVendorId, Guid id, CancellationToken cancellationToken)
    {
        // This is superfluous in MOST cases (this function is called only by Admin roles)
        if (role != UserRole.Admin && userVendorId != id)
            return null;

        var vendor = await _db.Vendors.FindAsync([id], cancellationToken);
        if (vendor is null)
            return null;

        return vendor;
    }
}

#pragma warning restore CA2208 // Instantiate argument exceptions correctly
