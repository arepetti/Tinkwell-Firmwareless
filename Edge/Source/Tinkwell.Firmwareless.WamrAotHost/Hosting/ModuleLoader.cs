using Microsoft.Extensions.Logging;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class ModuleLoader(ILogger<ModuleLoader> logger) : IModuleLoader, IDisposable
{
    public void Load(string[] paths)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Initialize();
        LoadModules(paths);
        StartInstances();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private readonly ILogger<ModuleLoader> _logger = logger;
    private readonly Dictionary<string, WasmInstance> _instances = new();
    private bool _disposed;

    private void Initialize()
    {
        _logger.LogInformation("Initializing WAMR runtime...");
        WamrHost.Initialize();

        _logger.LogDebug("Registering native functions...");
        WamrHost.RegisterNativeFunctions([
            WamrHost.MakeNativeSymbol("abort",
                new HostExportedFunctions.NativeAbortDelegate(HostExportedFunctions.Abort),
                WamrHost.Signature(typeof(void), typeof(nint), typeof(nint), typeof(int), typeof(int))),

            WamrHost.MakeNativeSymbol("tw_log",
                new HostExportedFunctions.NativeLogDelegate(HostExportedFunctions.Log),
                WamrHost.Signature(typeof(int), typeof(int), typeof(nint), typeof(int), typeof(nint), typeof(int))),

            WamrHost.MakeNativeSymbol("tw_mqtt_publish",
                new HostExportedFunctions.NativeMqttPublishDelegate(HostExportedFunctions.MqttPublish),
                WamrHost.Signature(typeof(int), typeof(nint), typeof(int), typeof(nint), typeof(int)))
        ]);
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
        _logger.LogInformation("Initializing...");
        foreach (var inst in _instances)
            WamrHost.CallExportSV(inst.Value, inst.Value.OnInitializeFunc, inst.Key);

        _logger.LogInformation("Starting...");
        foreach (var inst in _instances)
        {
            _logger.LogTrace("Starting {Name}...", inst.Key);
            WamrHost.CallExportVV(inst.Value, inst.Value.OnStartFunc);
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
