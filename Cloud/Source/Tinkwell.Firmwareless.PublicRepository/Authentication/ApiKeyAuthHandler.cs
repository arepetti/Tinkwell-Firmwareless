using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using Tinkwell.Firmwareless.PublicRepository.Services;

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
        IApiKeyValidator validator) : base(options, logger, encoder, clock)
    {
        _validator = validator;
    }
#pragma warning restore CS0618 // Type or member is obsolete

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided))
            return AuthenticateResult.NoResult();

        return await _validator.ValidateAsync(provided.ToString(), Scheme, Context.RequestAborted);
    }

    private readonly IApiKeyValidator _validator;
}
