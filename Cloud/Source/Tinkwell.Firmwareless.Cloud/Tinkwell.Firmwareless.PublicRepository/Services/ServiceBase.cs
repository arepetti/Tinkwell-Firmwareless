using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Repositories;

public abstract class ServiceBase(AppDbContext db)
{
    protected async Task<FindResponse<TDto>> FindAllAsync<TEntity, TDto>(
        ClaimsPrincipal user,
        string requiredScope,
        IQueryable<TEntity> query,
        FindRequest request,
        Func<TEntity, TDto> select,
        CancellationToken cancellationToken
    )
        where TEntity : EntityBase
    {
        Debug.Assert(user is not null);
        Debug.Assert(request is not null);

        var (role, scopes, vendorId) = user.GetScopesAndVendorId();

        if (role == UserRole.None)
            throw new UnauthorizedAccessException("User must be authenticated to list this resource.");

        if (!scopes.Contains(requiredScope))
            throw new ForbiddenAccessException("You do not have the permission to read this resource.");

        if (request.PageIndex is not null && request.PageIndex < 0)
            throw new ArgumentException("Page index must be greater than or equal to 0.", nameof(request.PageIndex));

        if (request.PageLength is not null && request.PageLength <= 0)
            throw new ArgumentException("Page length must be greater than 0.", nameof(request.PageIndex));

        if (request.PageLength is not null && request.PageLength > FindRequest.MaximumPageLength)
            throw new ArgumentException($"Page length must be less than {FindRequest.MaximumPageLength}.", nameof(request.PageIndex));

        int pageIndex = request.PageIndex ?? 0;
        int pageLength = request.PageLength ?? FindRequest.DefaultPageLength;

        if (string.IsNullOrWhiteSpace(request.Sort))
            query = query.ApplySorting(request.Sort);
        else
            query = query.OrderByDescending(x => x.CreatedAt);

        var items = query
            .ApplyFilters(request.Filter)
            .Skip(pageIndex * pageLength)
            .Take(pageLength)
            .Select(select);

        var total = await query.CountAsync(cancellationToken);
        return new FindResponse<TDto>(items.ToList(), total, pageIndex, pageLength);
    }

    protected async Task SaveChangesAsync(CancellationToken cancellationToken)
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

    private readonly AppDbContext _db = db;
}
