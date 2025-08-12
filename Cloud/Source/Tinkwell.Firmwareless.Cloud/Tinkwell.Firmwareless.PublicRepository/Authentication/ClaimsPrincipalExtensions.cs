using System.Security.Claims;

namespace Tinkwell.Firmwareless.PublicRepository.Authentication;

enum UserRole
{
    None,
    User,
    Admin
}

static class ClaimsPrincipalExtensions
{
    public static (UserRole Role, HashSet<string> Scopes, Guid? VendorId) GetScopesAndVendorId(this ClaimsPrincipal user)
    {
        if (user.Identity is not { IsAuthenticated: true })
            return (UserRole.None, [], null);

        if (user.IsInRole("Admin"))
            return (UserRole.Admin, [], null);

        var scopes = user.FindAll("scope").Select(c => c.Value).ToHashSet();
        var vendorIdClaim = user.FindFirst(CustomClaimTypes.VendorId)?.Value;
        if (vendorIdClaim is null)
            return (UserRole.None, scopes, null);

        if (!Guid.TryParse(vendorIdClaim, out var vendorId))
            return (UserRole.None, scopes, null);

        return (UserRole.User, scopes, vendorId);
    }
}
