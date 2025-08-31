namespace Tinkwell.Firmwareless.Config;

public interface IConfigFileReader<TConfig> where TConfig : class, new()
{
    Task<TConfig> ReadAsync(string filePath, ConfigFileReaderOptions options, CancellationToken cancellationToken);

    Task<TConfig> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        => ReadAsync(filePath, new(), cancellationToken);
}
