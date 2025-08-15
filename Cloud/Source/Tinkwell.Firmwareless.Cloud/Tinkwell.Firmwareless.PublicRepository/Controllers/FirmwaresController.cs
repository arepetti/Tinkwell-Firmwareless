using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Repositories;
using Tinkwell.Firmwareless.PublicRepository.Services;
using Tinkwell.Firmwareless.PublicRepository.Services.Queries;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("api/v1/firmwares")]
public sealed class FirmwaresController(ILogger<FirmwaresController> logger, FirmwaresService service, CompilationProxyService compilationProxy)
    : TinkwellControllerBase(logger)
{
    public sealed record DownloadRequest(Guid VendorId, Guid ProductId, FirmwareType Type, string HardwareVersion, string HardwareArchitecture);

    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FirmwaresService.View>> Create([FromForm] FirmwaresService.CreateRequest req, CancellationToken ct)
    {
        return await Try(async () =>
        {
            var entity = await _service.CreateAsync(User, req, ct);
            return CreatedAtAction(nameof(Find), new { id = entity.Id }, entity);
        });
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<FindResponse<FirmwaresService.View>>> FindAll(
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
    public async Task<ActionResult<FirmwaresService.View>> Find(Guid id, CancellationToken ct)
    {
        return await Try(async () =>
        {
            return Ok(await _service.FindAsync(HttpContext.User, id, ct));
        });
    }

    [HttpPut]
    [Authorize]
    public async Task<ActionResult<FirmwaresService.View>> Update(FirmwaresService.UpdateRequest req, CancellationToken ct)
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

    [HttpPost("download")]
    [Authorize]
    public async Task<IActionResult> Download([FromBody] DownloadRequest request, CancellationToken ct)
    {
        return await Try(async () =>
        {
            _logger.LogInformation("Download request for firmware: VendorId={VendorId}, ProductId={ProductId}, Type={Type}, HardwareVersion={HardwareVersion}, HardwareArchitecture={HardwareArchitecture}",
                request.VendorId, request.ProductId, request.Type, request.HardwareVersion, request.HardwareArchitecture);
            var blobName = await _service.GetBlobName(
                HttpContext.User,
                new FirmwaresService.ResolveBlobNameRequest(request.VendorId, request.ProductId, request.Type, request.HardwareVersion),
                ct);

            _logger.LogInformation("Compiling {Name} for HardwareArchitecture={HardwareArchitecture}", blobName, request.HardwareArchitecture);
            var compilationRequest = new CompilationProxyService.Request(blobName, request.HardwareArchitecture);
            var stream = await _compilationProxy.CompileAsync(compilationRequest, ct);

            Response.Headers.CacheControl = "no-store";
            return File(stream, MediaTypeNames.Application.Octet, blobName);
        });
    }

    private readonly ILogger<FirmwaresController> _logger = logger;
    private readonly FirmwaresService _service = service;
    private readonly CompilationProxyService _compilationProxy = compilationProxy;

}
