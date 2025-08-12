using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tinkwell.Firmwareless.PublicRepository.Database;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("api/v1/vendors")]
public sealed class VendorsController : ControllerBase
{
    public VendorsController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateVendorRequest(string Name);
    public record VendorView(Guid Id, string Name, DateTimeOffset CreatedAt);

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<VendorView>> Create(CreateVendorRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        var id = Guid.NewGuid();
        
        var vendor = await _db.Vendors.Where(x => x.Name == req.Name).FirstOrDefaultAsync(ct);
        if (vendor is not null)
            return BadRequest("A vendor with the same name already exists.");

        var entity = new Vendor
        {
            Id = id,
            Name = req.Name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Vendors.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id },
            new VendorView(entity.Id, entity.Name, entity.CreatedAt));
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<IEnumerable<VendorView>> List(CancellationToken ct)
        => await _db.ApiKeys
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new VendorView(k.Id, k.Name, k.CreatedAt))
            .ToListAsync(ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<VendorView>> GetById(Guid id, CancellationToken ct)
    {
        var k = await _db.Vendors.FindAsync([id], ct);
        if (k is null)
            return NotFound();

        return new VendorView(k.Id, k.Name, k.CreatedAt);
    }

    private readonly AppDbContext _db;
}
