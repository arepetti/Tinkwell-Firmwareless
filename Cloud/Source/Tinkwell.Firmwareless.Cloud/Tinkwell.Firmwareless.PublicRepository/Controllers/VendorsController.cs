using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tinkwell.Firmwareless.PublicRepository.Repositories;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("api/v1/vendors")]
public sealed class VendorsController(ILogger<VendorsController> logger, VendorService service) : TinkwellControllerBase(logger)
{
    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<VendorService.VendorView>> Create(VendorService.CreateRequest req, CancellationToken ct)
    {
        return await Try(async () =>
        {
            var entity = await _service.CreateAsync(User, new VendorService.CreateRequest(req.Name, req.Notes), ct);
            return CreatedAtAction(nameof(Find), new { id = entity.Id }, entity);
        });
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<FindResponse<KeyService.KeyView>>> FindAll(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageLength = 20,
        [FromQuery] string? filter = null,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        return await Try(async () =>
        {
            var request = new FindRequest(pageIndex, pageLength, filter, sort);
            return Ok(await _service.FindAllAsync(HttpContext.User, request, ct));
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<KeyService.KeyView>> Find(Guid id, CancellationToken ct)
    {
        return await Try(async () =>
        {
            return Ok(await _service.FindAsync(HttpContext.User, id, ct));
        });
    }

    [HttpPut]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<VendorService.VendorView>> Update(VendorService.UpdateRequest req, CancellationToken ct)
    {
        return await Try(async () =>
            Ok(await _service.UpdateAsync(User, new VendorService.UpdateRequest(req.Id, req.Name, req.Notes), ct))
        );
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        return await Try(async () =>
        {
            await _service.DeleteAsync(HttpContext.User, id, ct);
            return NoContent();
        });
    }

    private readonly VendorService _service = service;
}
