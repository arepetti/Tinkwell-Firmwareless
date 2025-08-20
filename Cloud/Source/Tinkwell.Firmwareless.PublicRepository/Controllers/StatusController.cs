using Microsoft.AspNetCore.Mvc;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("status")]
public class StatusController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("OK");
}
