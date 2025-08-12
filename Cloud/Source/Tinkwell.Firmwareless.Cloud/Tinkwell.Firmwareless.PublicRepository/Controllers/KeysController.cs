using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tinkwell.Firmwareless.PublicRepository.Repositories;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("api/v1/keys")]
public sealed class KeysController(ILogger<KeysController> logger, KeyService service) : TinkwellControllerBase(logger)
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<KeyService.KeyView>> Create(KeyService.CreateKeyRequest request, CancellationToken ct)
    {
        return await Try(async () =>
        {
            var response = await _service.CreateAsync(HttpContext.User, request, ct);
            return CreatedAtAction(nameof(Find), new { id = response.Id }, response);
        });
    }

    [HttpGet]
    [Authorize]
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

    [HttpDelete("{id:guid}/revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        return await Try(async () =>
        {
            await _service.RevokeAsync(HttpContext.User, id, ct);
            return NoContent();
        });
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        return await Try(async () =>
        {
            await _service.DeleteAsync(HttpContext.User, id, ct);
            return NoContent();
        });
    }

    private readonly KeyService _service = service;
}
