namespace Tinkwell.Firmwareless.WasmHost.Packages;

sealed class TemporaryFile : IDisposable
{
    public TemporaryFile()
    {
        Path = System.IO.Path.GetTempFileName();
    }
    public string Path { get; }
    public void Dispose()
    {
        try
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
        catch
        {
            // Ignore
        }
    }
}

