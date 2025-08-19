using Microsoft.Extensions.Logging;
using Tinkwell.Firmwareless.PublicRepository.Services;

namespace Tinkwell.Firmwareless.PublicRepository.IntegrationTests.Fakes;

public class FakeCompilationProxyService : CompilationProxyService
{
    public FakeCompilationProxyService(IHttpClientFactory factory, ILogger<CompilationProxyService> logger) : base(factory, logger)
    {
    }

    public MemoryStream ResponseStream { get; set; } = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("fake firmware"));
    public bool ShouldThrow { get; set; }
    public Request? LastRequest { get; private set; }

    public override Task<Stream> CompileAsync(Request request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (ShouldThrow)
        {
            throw new HttpRequestException("Simulated compilation server error.");
        }

        // Reset stream position to be read by the controller
        ResponseStream.Position = 0;
        return Task.FromResult<Stream>(ResponseStream);
    }
}
