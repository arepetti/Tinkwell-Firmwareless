using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("api/v1/keys")]
public sealed class KeysController : ControllerBase
{
    public KeysController(AppDbContext db, IOptions<ApiKeyOptions> opts)
    {
        _db = db;
        _opts = opts.Value;
    }

    public record CreateKeyRequest(Guid VendorId, string Name, string Role, int DaysValid, string[]? Scopes);
    public record CreateKeyResponse(Guid Id, string Name, string Role, string[] Scopes, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, string PlaintextKey);
    public record KeyView(Guid Id, Guid? VendorId, string Name, string Role, string[] Scopes, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt);

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<CreateKeyResponse>> Create(CreateKeyRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        if (req.Role is not ("User" or "Admin"))
            return BadRequest("Role must be User or Admin.");

        if (req.DaysValid < 1 || req.DaysValid > 365)
            return BadRequest("DaysValid must be between 1 and 365.");

        var id = Guid.NewGuid();
        var plaintext = ApiKeyFormat.Generate(id, _opts);
        var (hash, salt) = ApiKeyHasher.Hash(plaintext);

        var vendor = await _db.Vendors.FindAsync([req.VendorId], ct);
        if (vendor is null)
            return NotFound("Vendor not found.");

        var entity = new ApiKey
        {
            Id = id,
            Name = req.Name,
            Role = req.Role,
            Hash = hash,
            Salt = salt,
            Scopes = req.Scopes is null ? "" : string.Join(',', req.Scopes),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(req.DaysValid),

            Vendor = vendor
        };

        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id },
            new CreateKeyResponse(entity.Id, entity.Name, entity.Role, req.Scopes ?? [], entity.CreatedAt, entity.ExpiresAt, plaintext));
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<IEnumerable<KeyView>> List(CancellationToken ct)
        => await _db.ApiKeys
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new KeyView(k.Id, k.VendorId, k.Name, k.Role,
                string.IsNullOrEmpty(k.Scopes) ? Array.Empty<string>() : k.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                k.CreatedAt, k.ExpiresAt, k.RevokedAt))
            .ToListAsync(ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<KeyView>> GetById(Guid id, CancellationToken ct)
    {
        var k = await _db.ApiKeys.FindAsync([id], ct);
        if (k == null) return NotFound();

        return new KeyView(k.Id, k.VendorId, k.Name, k.Role,
            string.IsNullOrEmpty(k.Scopes) ? [] : k.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            k.CreatedAt, k.ExpiresAt, k.RevokedAt);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admins")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var apiKey = await _db.ApiKeys.FindAsync([id], ct);

        if (apiKey is null)
            return NotFound();

        if (apiKey.RevokedAt is not null)
            return NoContent();

        apiKey.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private readonly AppDbContext _db;
    private readonly ApiKeyOptions _opts;
}
