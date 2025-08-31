using Microsoft.Extensions.Logging;
using Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class HostService(ILogger<HostService> logger, IModuleLoader loader)
{
    public void Start(string path, bool transient)
    {
        var wasmFiles = Find(path, "*.wasm");
        var aotFiles = Find(path, "*.aot");

        _loader.Load(Enumerable.Concat(wasmFiles, aotFiles).ToArray());

        if (!transient)
            WaitForTermination();
    }

    private readonly ILogger<HostService> _logger = logger;
    private readonly IModuleLoader _loader = loader;

    private string[] Find(string path, string searchPattern)
    {
        var files = Directory.GetFiles(path, searchPattern).ToArray();
        _logger.LogInformation("Found {Count} {Extension} files in {Path}", files.Length, Path.GetExtension(searchPattern), path);
        return files;
    }

    private void WaitForTermination()
    {
        if (Console.IsInputRedirected)
        {
            Console.WriteLine("Press CTRL+C to exit");
            do
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.C && key.Modifiers == ConsoleModifiers.Control)
                    break;
            } while (true);
        }
        else
        {
            Console.WriteLine("Press any key to exit");
            Console.Read();
        }
    }
}