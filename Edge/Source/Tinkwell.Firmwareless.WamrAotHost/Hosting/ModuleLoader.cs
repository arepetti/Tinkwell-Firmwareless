using Microsoft.Extensions.Logging;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class ModuleLoader(ILogger<ModuleLoader> logger, IRegisterHostUnsafeNativeFunctions exportedFunctions) : IModuleLoader, IDisposable
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
        StartInstances();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private readonly ILogger<ModuleLoader> _logger = logger;
    private readonly IRegisterHostUnsafeNativeFunctions _exportedFunctions = exportedFunctions;
    private readonly Dictionary<string, WasmInstance> _instances = new();
    private bool _disposed;

    private void Initialize()
    {
        _logger.LogInformation("Initializing WAMR runtime...");
        WamrHost.Initialize();

        _logger.LogDebug("Registering native functions...");
        _exportedFunctions.RegisterAll();
    }

    private void LoadModules(string[] paths)
    {
        foreach (var path in paths)
        {
            var id = $"_{_instances.Count}_{Path.GetFileNameWithoutExtension(path)}_";
            _logger.LogInformation("Loading module {Path}", path);
            var inst = WamrHost.LoadInstance(id, path);
            _instances[id] = inst;
        }
    }

    private void StartInstances()
    {
        _logger.LogDebug("Initializing...");
        foreach (var inst in _instances)
            WamrHost.CallExportSV(inst.Value, inst.Value.OnInitializeFunc, inst.Key);

        _logger.LogDebug("Starting...");
        foreach (var inst in _instances)
        {
            _logger.LogTrace("Starting {Name}...", inst.Key);
            WamrHost.CallExportVV(inst.Value, inst.Value.OnStartFunc, required: true);
        }
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            _logger.LogInformation("Terminating...");

            foreach (var inst in _instances)
                WamrHost.CallExportVV(inst.Value, inst.Value.OnDisposeFunc);

            foreach (var inst in _instances.Values)
                inst.Dispose();

            WamrHost.Shutdown();
            _logger.LogDebug("Terminated");
        }
        finally
        {
            _disposed = true;
        }
    }
}
