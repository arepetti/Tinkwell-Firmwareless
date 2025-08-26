using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tinkwell.Firmwareless.Cloud.Security;
using Tinkwell.Firmwareless.Controllers;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("/api/v1/repository")]
public class RepositoryController(ILogger<RepositoryController> logger) : TinkwellControllerBase(logger)
{
    public sealed record UserInfo(string Auth, string Name, string Role, string[] Scopes, Guid? VendorId);

    [HttpGet("identity")]
    public Task<ActionResult> Get([FromServices] IKeyVaultSignatureService service)
    {
        return Try(async () =>
        {
            return Ok(await service.GetPublicKeyAsync(CancellationToken.None));
        });

    }
}
