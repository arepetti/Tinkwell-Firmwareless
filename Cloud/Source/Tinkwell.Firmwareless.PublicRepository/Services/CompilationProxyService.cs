using System.Net.Mime;
using Tinkwell.Firmwareless.Controllers;

namespace Tinkwell.Firmwareless.PublicRepository.Services;

public class CompilationProxyService
{
    public CompilationProxyService(IHttpClientFactory factory, ILogger<CompilationProxyService> logger)
    {
        _httpClient = factory.CreateClient("tinkwell-compilation-server");
    }

    public virtual async Task<Stream> CompileAsync(CompilationRequest request, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(1)); // Hard-coded 1 minute for now

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/compiler/compile")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType != MediaTypeNames.Application.Zip)
            throw new HttpRequestException($"Unexpected content type: {response.Content.Headers.ContentType?.MediaType}");

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    private readonly HttpClient _httpClient;
}