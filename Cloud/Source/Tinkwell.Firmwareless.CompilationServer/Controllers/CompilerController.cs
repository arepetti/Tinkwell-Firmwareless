using Microsoft.AspNetCore.Mvc;
using Tinkwell.Firmwareless.CompilationServer.Services;
using Tinkwell.Firmwareless.Controllers;

namespace Tinkwell.Firmwareless.CompilationServer.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CompilerController : TinkwellControllerBase
{
    public CompilerController(ICompilationService compilationService, ILogger<CompilerController> logger) : base(logger)
    {
        _compilationService = compilationService;
        _logger = logger;
    }

    [HttpPost("compile")]
    public async Task<IActionResult> Compile([FromBody] CompilationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received compilation request for architecture {Architecture}", request.Architecture);

        if (string.IsNullOrWhiteSpace(request.BlobName) || string.IsNullOrWhiteSpace(request.Architecture))
            return BadRequest("BlobName and Architecture are required.");

        try
        {
            var resultStream = await _compilationService.CompileAsync(request, cancellationToken);
            
            _logger.LogInformation("Compilation completed, returning ZIP archive.");
            return File(resultStream, "application/zip", $"compilation-result-{request.Architecture}.zip");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Unsupported architecture requested: {Architecture}", request.Architecture);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during compilation request.");
            return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
        }
    }

    private readonly ICompilationService _compilationService;
    private readonly ILogger<CompilerController> _logger;
}
