using Microsoft.Extensions.Logging;
using Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class HostService(ILogger<HostService> logger, IModuleLoader loader)
{
    public void Start(string path, bool transient)
    {

        _loader.Load(FindAllSourceFiles(path));
        _loader.InitializeModules();

        if (!transient)
            WaitForTermination();
    }

    private readonly ILogger<HostService> _logger = logger;
    private readonly IModuleLoader _loader = loader;

    private string[] FindAllSourceFiles(string path)
    {
        var wasmFiles = FindSourceFiles(path, "*.wasm");
        var aotFiles = FindSourceFiles(path, "*.aot");
        return Enumerable.Concat(wasmFiles, aotFiles).ToArray();
    }

    private string[] FindSourceFiles(string path, string searchPattern)
    {
        var files = Directory.GetFiles(path, searchPattern).ToArray();
        _logger.LogDebug("Found {Count} {Extension} file(s) in {Path}", files.Length, Path.GetExtension(searchPattern), path);
        return files;
    }

    private static void WaitForTermination()
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