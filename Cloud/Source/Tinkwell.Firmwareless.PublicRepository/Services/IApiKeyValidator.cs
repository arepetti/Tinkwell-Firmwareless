using Microsoft.AspNetCore.Authentication;

namespace Tinkwell.Firmwareless.PublicRepository.Services;

public interface IApiKeyValidator
{
    Task<AuthenticateResult> ValidateAsync(string presentedApiKey, string scheme, CancellationToken cancellationToken);
}
