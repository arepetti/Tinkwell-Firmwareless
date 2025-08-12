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
    public async Task<ActionResult<VendorService.View>> Create(VendorService.CreateRequest req, CancellationToken ct)
    {
        return await Try(async () =>
        {
            var entity = await _service.CreateAsync(User, req, ct);
            return CreatedAtAction(nameof(Find), new { id = entity.Id }, entity);
        });
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<FindResponse<VendorService.View>>> FindAll(
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
    public async Task<ActionResult<VendorService.View>> Find(Guid id, CancellationToken ct)
    {
        return await Try(async () =>
        {
            return Ok(await _service.FindAsync(HttpContext.User, id, ct));
        });
    }

    [HttpPut]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<VendorService.View>> Update(VendorService.UpdateRequest req, CancellationToken ct)
    {
        return await Try(async () =>
            Ok(await _service.UpdateAsync(User, req, ct))
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
