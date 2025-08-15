namespace Tinkwell.Firmwareless.CompilationServer.Services;

public interface ICompilationService
{
    Task<Stream> CompileAsync(CompilationRequest request, CancellationToken cancellationToken);
}

public sealed record CompilationRequest(string BlobUrl, string Architecture);
