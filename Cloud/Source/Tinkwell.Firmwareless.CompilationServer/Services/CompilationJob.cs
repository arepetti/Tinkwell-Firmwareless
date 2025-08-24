using Tinkwell.Firmwareless.Controllers;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public sealed class CompilationJob : IDisposable
{
    public CompilationJob(CompilationRequest request)
    {
        Id = Guid.NewGuid().ToString("N");
        WorkingDirectoryPath = Path.Combine(Path.GetTempPath(), Id);
        Request = request;

        Directory.CreateDirectory(WorkingDirectoryPath);
        Directory.CreateDirectory(Path.Combine(WorkingDirectoryPath, "assets"));
    }

    public string Id { get; }

    public string WorkingDirectoryPath { get; }

    public CompilationRequest Request { get; }

    public CompilationManifest? Manifest { get; set; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkingDirectoryPath))
                Directory.Delete(WorkingDirectoryPath, recursive: true);
        }
        catch
        {
        }
    }
}
