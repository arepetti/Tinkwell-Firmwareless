using Microsoft.AspNetCore.Mvc;
using Tinkwell.Firmwareless.PublicRepository.Repositories;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

public abstract class TinkwellControllerBase(ILogger logger) : ControllerBase
{
    protected async Task<ActionResult> Try<T>(Func<Task<T>> func) where T : ActionResult
    {
        try
        {
            return await func();
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (UnauthorizedAccessException e)
        { 
            return Unauthorized(e.Message);
        }
        catch (NotFoundException e)
        {
            return NotFound(new ErrorResponse(e.Message));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new ErrorResponse(e.Message, e.ParamName));
        }
        catch (ConflictException e)
        {
            return Conflict(new ErrorResponse(e.Message));
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "Error calling another service: {Message}", e.Message);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse($"The service is currently unavailable. Please try again later. Details: {e.Message}"));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An unexpected error occurred: {Message}", e.Message);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse("An unexpected error occurred."));
        }
    }

    private readonly ILogger _logger = logger;
}
