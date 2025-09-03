using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class WamrHost(ILogger<WamrHost> logger, IRegisterHostUnsafeNativeFunctions exportedFunctions) : IWamrHost, IDisposable
{
    public void Load(string[] paths)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Initialize();
        LoadModules(paths);
    }

    public void InitializeModules()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("Initializing...");
        foreach (var inst in _instances)
            Wamr.CallExportSV(inst.Value, inst.Value.OnInitializeFunc, inst.Key);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("Starting...");
        foreach (var inst in _instances)
        {
            _logger.LogTrace("Starting {Name}...", inst.Key);
            Wamr.CallExportVV(inst.Value, inst.Value.OnStartFunc, required: true);
        }
    }

    public void Stop()
    {
        _logger.LogInformation("Terminating...");

        foreach (var inst in _instances)
            Wamr.CallExportVV(inst.Value, inst.Value.OnDisposeFunc);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private readonly ILogger<WamrHost> _logger = logger;
    private readonly IRegisterHostUnsafeNativeFunctions _exportedFunctions = exportedFunctions;
    private readonly Dictionary<string, WasmInstance> _instances = new();
    private bool _disposed;

    private void Initialize()
    {
        _logger.LogDebug("Initializing WAMR runtime...");
        Wamr.Initialize();

        _logger.LogDebug("Registering native functions...");
        _exportedFunctions.RegisterAll();
    }

    private void LoadModules(string[] paths)
    {
        for (int index=0; index < paths.Length; ++index)
        {
            string path = paths[index];
            var id = $"_{index}_{Path.GetFileNameWithoutExtension(path)}_";
            _logger.LogInformation("Loading module {Index} of {Count}: {Path}", index, paths.Length, path);
            var inst = Wamr.LoadInstance(id, path);
            _instances[id] = inst;
        }
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            foreach (var inst in _instances.Values)
                inst.Dispose();

            Wamr.Shutdown();
            _logger.LogDebug("WASM runtime has been disposed");
        }
        finally
        {
            _disposed = true;
        }
    }
}
