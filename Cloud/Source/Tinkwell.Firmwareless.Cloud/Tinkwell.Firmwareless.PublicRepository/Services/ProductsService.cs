using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Repositories;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

public sealed class ProductsService(AppDbContext db) : ServiceBase(db)
{
    public sealed record CreateRequest(Guid VendorId, string Name, string Model, ProductStatus Status);

    public sealed record UpdateRequest(Guid Id, string? Name, string? Model, ProductStatus? Status);

    public sealed record View(Guid Id, Guid VendorId, string Name, string Model, ProductStatus Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    public async Task<View> CreateAsync(ClaimsPrincipal user, CreateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);
        Debug.Assert(request is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();
        if (role != UserRole.Admin && !scopes.Contains(Scopes.ProductCreate))
            throw new ForbiddenAccessException();

        if (role != UserRole.Admin && request.VendorId != vendorId)
            throw new ForbiddenAccessException("User is not allowed to create products for this vendor.");

        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name, nameof(request.Name));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model, nameof(request.Model));

        var vendor = await _db.Vendors.FindAsync([request.VendorId], cancellationToken);
        if (vendor is null)
            throw new NotFoundException(request.VendorId.ToString(), nameof(request.VendorId));

        var entity = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Model = request.Model,
            Status = request.Status,
            CreatedAt = DateTimeOffset.UtcNow,
            Vendor = vendor,
        };

        await _db.Products.AddAsync(entity, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return EntityToView(entity);
    }

    public async Task<FindResponse<View>> FindAllAsync(ClaimsPrincipal user, FindRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);

        var (role, _, vendorId) = user.GetScopesAndVendorId();

        IQueryable<Product> query = _db.Products
            .AsNoTracking()
            .Include(x => x.Vendor);

        if (role != UserRole.Admin)
            query = query.Where(x => x.VendorId == vendorId);

        return await FindAllAsync(
            user,
            Scopes.ProductRead,
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

        if (!scopes.Contains(Scopes.ProductRead))
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
        if (!scopes.Contains(Scopes.ProductUpdate))
            throw new ForbiddenAccessException();

        var entity = await TryFindAsync(role, vendorId, request.Id, cancellationToken);
        if (entity is null)
            throw new NotFoundException(request.Id.ToString(), nameof(request.Id));

        if (request.Name is not null)
            entity.Name = request.Name;

        if (request.Model is not null)
            entity.Model = request.Model;

        if (request.Status is not null)
            entity.Status = request.Status.Value;

        await SaveChangesAsync(cancellationToken);
        return EntityToView(entity);
    }

    public async Task DeleteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        Debug.Assert(user is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();
        if (!scopes.Contains(Scopes.ProductDelete))
            throw new ForbiddenAccessException();

        var entity = await TryFindAsync(role, vendorId, id, cancellationToken);
        if (entity is null)
            throw new NotFoundException(id.ToString(), nameof(id));

        if (entity.VendorId != vendorId && role != UserRole.Admin)
            throw new ForbiddenAccessException("User is not allowed to delete products for this vendor.");

        _db.Products.Remove(entity);
        await SaveChangesAsync(cancellationToken);
    }

    private readonly AppDbContext _db = db;

    private static View EntityToView(Product entity)
    {
        return new View(
            entity.Id,
            entity.VendorId,
            entity.Name,
            entity.Model,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private async Task<Product?> TryFindAsync(UserRole role, Guid? userVendorId, Guid id, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .Include(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (product is not null && role != UserRole.Admin && userVendorId != product.VendorId)
            return null;

        return product;
    }
}

#pragma warning restore CA2208 // Instantiate argument exceptions correctly
