using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tinkwell.Firmwareless.Controllers;
using Tinkwell.Firmwareless.PublicRepository.Authentication;

namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

[ApiController]
[Route("/api/v1/users")]
public class UsersController(ILogger<UsersController> logger) : TinkwellControllerBase(logger)
{
    public sealed record UserInfo(string Auth, string Name, string Role, string[] Scopes, Guid? VendorId);

    [HttpGet("me")]
    [Authorize]
    public Task<ActionResult> Get()
    {
        return Try(() =>
        {
            var (role, scopes, vendorId) = HttpContext.User.GetScopesAndVendorId();
            var apiKeyId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var info = new UserInfo("API-Key", apiKeyId, role.ToString().ToLowerInvariant(), scopes.ToArray(), vendorId);
            return Task.FromResult(Ok(info));
        });

    }
}
