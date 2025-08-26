using Microsoft.AspNetCore.Mvc;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("/api/v1/status")]
public class StatusController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("OK");
}
