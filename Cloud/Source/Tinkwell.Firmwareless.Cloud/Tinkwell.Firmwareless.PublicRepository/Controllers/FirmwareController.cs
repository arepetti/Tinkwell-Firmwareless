using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tinkwell.Firmwareless.PublicRepository.Authentication;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("api/v1/firmware")]
public class FirmwareController : ControllerBase
{
    [HttpGet("{id}")]
    [AllowAnonymous]
    public IActionResult Get(string id) => Ok(new { id, status = "public" });

    [HttpPost]
    [Authorize(AuthenticationSchemes = ApiKeyAuthHandler.Scheme, Policy = "Publisher")]
    public IActionResult Publish([FromBody] object manifest) => Ok(new { status = "published" });
}
