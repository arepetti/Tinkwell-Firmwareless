namespace Tinkwell.Firmwareless.PublicRepository.Services;

sealed class TemporaryFile : IDisposable
{
    public TemporaryFile()
    {
        Path = System.IO.Path.GetTempFileName();
    }

    public string Path { get; }

    public async Task WriteFromStream(Stream stream, CancellationToken cancellationToken)
    {
        using var fileStream = File.OpenWrite(Path);
        await stream.CopyToAsync(fileStream, cancellationToken);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
        catch
        {
        }
    }
}
