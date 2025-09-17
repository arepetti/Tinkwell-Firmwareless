using Microsoft.AspNetCore.Mvc;
using Tinkwell.Firmwareless.Cloud.Security;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("/api/v1/repository")]
public class RepositoryController(ILogger<RepositoryController> logger) : ControllerBase
{
    public sealed record UserInfo(string Auth, string Name, string Role, string[] Scopes, Guid? VendorId);

    [HttpGet("identity")]
    public async Task<ActionResult> Get([FromServices] IKeyVaultSignatureService service)
    {
        return Ok(await service.GetPublicKeyAsync(CancellationToken.None));
    }
}
