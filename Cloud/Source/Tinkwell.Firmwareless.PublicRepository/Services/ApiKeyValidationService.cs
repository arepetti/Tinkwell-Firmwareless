using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Configuration;
using Tinkwell.Firmwareless.PublicRepository.Database;

namespace Tinkwell.Firmwareless.PublicRepository.Services;

public class ApiKeyValidationService(AppDbContext db, IOptions<ApiKeyOptions> settings) : IApiKeyValidator
{
    public async Task<AuthenticateResult> ValidateAsync(string presentedApiKey, string scheme, CancellationToken cancellationToken)
    {
        // Pre-DB validation (prefix, Base64Url, HMAC)
        if (!ApiKeyFormat.TryParseAndValidate(presentedApiKey, _settings, out var keyId))
            return AuthenticateResult.Fail("Invalid API key signature.");

        // Single-row lookup by Id
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(x => x.Id == keyId, cancellationToken);
        if (apiKey is null || apiKey.RevokedAt is not null || (apiKey.ExpiresAt is not null && apiKey.ExpiresAt <= DateTimeOffset.UtcNow))
            return AuthenticateResult.Fail("API key inactive.");

        // Verify the exact key value via salted hash
        var computed = ApiKeyHasher.HashWithSalt(presentedApiKey, apiKey.Salt);
        if (!ApiKeyHasher.FixedTimeEquals(computed, apiKey.Hash))
            return AuthenticateResult.Fail("Invalid API key.");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
            new Claim(ClaimTypes.Name, apiKey.Name),
            new Claim(ClaimTypes.Role, apiKey.Role)
        };

        if (!string.IsNullOrWhiteSpace(apiKey.Scopes))
        {
            foreach (var s in apiKey.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                claims.Add(new Claim("scope", s));
        }

        if (apiKey.VendorId is not null)
            claims.Add(new Claim(CustomClaimTypes.VendorId, apiKey.VendorId.ToString()!));

        var identity = new ClaimsIdentity(claims, scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, scheme);
        return AuthenticateResult.Success(ticket);
    }

    private readonly AppDbContext _db = db;
    private readonly ApiKeyOptions _settings = settings.Value;
}
