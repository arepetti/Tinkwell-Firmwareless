using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tinkwell.Firmwareless.PublicRepository.Repositories;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(ILogger<VendorsController> logger, ProductsService service) : TinkwellControllerBase(logger)
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProductsService.View>> Create(ProductsService.CreateRequest req, CancellationToken ct)
    {
        return await Try(async () =>
        {
            var entity = await _service.CreateAsync(User, req, ct);
            return CreatedAtAction(nameof(Find), new { id = entity.Id }, entity);
        });
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
        return await Try(async () =>
        {
            var request = new FindRequest(pageIndex, pageLength, filter, sort);
            return Ok(await _service.FindAllAsync(HttpContext.User, request, ct));
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ProductsService.View>> Find(Guid id, CancellationToken ct)
    {
        return await Try(async () =>
        {
            return Ok(await _service.FindAsync(HttpContext.User, id, ct));
        });
    }

    [HttpPut]
    [Authorize]
    public async Task<ActionResult<ProductsService.View>> Update(ProductsService.UpdateRequest req, CancellationToken ct)
    {
        return await Try(async () =>
            Ok(await _service.UpdateAsync(User, req, ct))
        );
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

    private readonly ProductsService _service = service;
}
