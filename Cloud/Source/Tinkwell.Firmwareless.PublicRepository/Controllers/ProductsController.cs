using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tinkwell.Firmwareless.Controllers;
using Tinkwell.Firmwareless.PublicRepository.Services;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(ProductsService service) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProductsService.View>> Create(ProductsService.CreateRequest req, CancellationToken ct)
    {
        var entity = await _service.CreateAsync(User, req, ct);
        return CreatedAtAction(nameof(Find), new { id = entity.Id }, entity);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<FindResponse<ProductsService.View>>> FindAll(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageLength = 20,
        [FromQuery] string? filter = null,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        var request = new FindRequest(pageIndex, pageLength, filter, sort);
        return Ok(await _service.FindAllAsync(HttpContext.User, request, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ProductsService.View>> Find(Guid id, CancellationToken ct)
    {
        return Ok(await _service.FindAsync(HttpContext.User, id, ct));
    }

    [HttpPut]
    [Authorize]
    public async Task<ActionResult<ProductsService.View>> Update(ProductsService.UpdateRequest req, CancellationToken ct)
    {
        return Ok(await _service.UpdateAsync(User, req, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(HttpContext.User, id, ct);
        return NoContent();
    }

    private readonly ProductsService _service = service;
}
