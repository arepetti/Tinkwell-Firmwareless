using Tinkwell.Firmwareless.Controllers;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public interface ICompilationService
{
    Task<Stream> CompileAsync(CompilationRequest request, CancellationToken cancellationToken);
}

