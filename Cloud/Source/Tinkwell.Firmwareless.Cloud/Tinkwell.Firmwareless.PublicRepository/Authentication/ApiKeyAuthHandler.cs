using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Tinkwell.Firmwareless.PublicRepository.Database;

namespace Tinkwell.Firmwareless.PublicRepository.Authentication;

public sealed class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public new const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";

#pragma warning disable CS0618 // Type or member is obsolete
    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        AppDbContext db,
        IOptions<ApiKeyOptions> settings) : base(options, logger, encoder, clock)
    {
        _db = db;
        _settings = settings.Value;
    }
#pragma warning restore CS0618 // Type or member is obsolete

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided))
            return AuthenticateResult.NoResult();

        var presented = provided.ToString();

        // Pre-DB validation (prefix, Base64Url, HMAC)
        if (!ApiKeyFormat.TryParseAndValidate(presented, _settings, out var keyId))
            return AuthenticateResult.Fail("Invalid API key signature.");

        // Single-row lookup by Id
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(x => x.Id == keyId, Context.RequestAborted);
        if (apiKey is null || apiKey.RevokedAt is not null || (apiKey.ExpiresAt is not null && apiKey.ExpiresAt <= DateTimeOffset.UtcNow))
            return AuthenticateResult.Fail("API key inactive.");

        // Verify the exact key value via salted hash
        var computed = ApiKeyHasher.HashWithSalt(presented, apiKey.Salt);
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

        var identity = new ClaimsIdentity(claims, Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme);
        return AuthenticateResult.Success(ticket);
    }

    private readonly AppDbContext _db;
    private readonly ApiKeyOptions _settings;
}
