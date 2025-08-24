namespace Tinkwell.Firmwareless.CompilationServer.Services;

sealed class CompilationJob : IDisposable
{
    public CompilationJob()
    {
        Id = Guid.NewGuid().ToString("N");
        WorkingDirectoryPath = Path.Combine(Path.GetTempPath(), Id);

        Directory.CreateDirectory(WorkingDirectoryPath);
        Directory.CreateDirectory(Path.Combine(WorkingDirectoryPath, "assets"));
    }

    public string Id { get; }

    public string WorkingDirectoryPath { get; }

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
