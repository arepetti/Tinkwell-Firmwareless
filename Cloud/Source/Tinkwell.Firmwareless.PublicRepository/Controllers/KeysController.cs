using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tinkwell.Firmwareless.Controllers;
using Tinkwell.Firmwareless.PublicRepository.Services;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("api/v1/keys")]
public sealed class KeysController(ILogger<KeysController> logger, KeysService service) : TinkwellControllerBase(logger)
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<KeysService.View>> Create(KeysService.CreateRequest request, CancellationToken ct)
    {
        return await Try(async () =>
        {
            var response = await _service.CreateAsync(HttpContext.User, request, ct);
            return CreatedAtAction(nameof(Find), new { id = response.Id }, response);
        });
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<FindResponse<KeysService.View>>> FindAll(
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
    public async Task<ActionResult<KeysService.View>> Find(Guid id, CancellationToken ct)
    {
        return await Try(async () =>
        {
            return Ok(await _service.FindAsync(HttpContext.User, id, ct));
        });
    }

    [HttpPost("this/revoke")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> RevokeThis(CancellationToken ct)
    {
        return await Try(async () =>
        {
            Guid id = Guid.Parse(HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _service.RevokeAsync(HttpContext.User, id, ct);
            return NoContent();
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

    private readonly KeysService _service = service;
}
