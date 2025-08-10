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
        var k = await _db.ApiKeys.FirstOrDefaultAsync(x => x.Id == keyId, Context.RequestAborted);
        if (k is null || k.RevokedAt is not null || (k.ExpiresAt is not null && k.ExpiresAt <= DateTimeOffset.UtcNow))
            return AuthenticateResult.Fail("API key inactive.");

        // Verify the exact key value via salted hash
        var computed = ApiKeyHasher.HashWithSalt(presented, k.Salt);
        if (!ApiKeyHasher.FixedTimeEquals(computed, k.Hash))
            return AuthenticateResult.Fail("Invalid API key.");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, k.Id.ToString()),
            new Claim(ClaimTypes.Name, k.Name),
            new Claim(ClaimTypes.Role, k.Role)
        };

        if (!string.IsNullOrWhiteSpace(k.Scopes))
        {
            foreach (var s in k.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                claims.Add(new Claim("scope", s));
        }

        var identity = new ClaimsIdentity(claims, Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme);
        return AuthenticateResult.Success(ticket);
    }

    private readonly AppDbContext _db;
    private readonly ApiKeyOptions _settings;
}
