namespace Tinkwell.Firmwareless.CompilationServer.Services;

static class DefaultLimits
{
    public const long MaximumFirmwareSize = 16 * 1024 * 1024; // 16 MB
    public const long MaximumAssetSize = 4 * 1024 * 1024; // 4 MB
    public const int MaxiumCompilationUnits = 10;
    public const int MaximumAssets = 100;

    public const long Memory = 1073741824; // 1 GB
    public const long MemorySwap = 1073741824; // 1 GB (disables swap)
    public const long NanoCpus = 1000000000; // 1 CPU core
    public const int Pids = 100; // Limit to 100 processes
    public const int Files = 1024; // Limit to 1024 open files
}
