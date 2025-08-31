namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class WasmInstance : IDisposable
{
    public readonly string Id;
    public readonly IntPtr Module;
    public readonly IntPtr Instance;
    public readonly IntPtr ExecEnv;
    public readonly bool Wasm64;
    public required IntPtr OnInitializeFunc;
    public required IntPtr OnStartFunc;
    public required IntPtr OnDisposeFunc;
    public required IntPtr OnMessageFunc;

    public WasmInstance(string id, IntPtr module, IntPtr instance, IntPtr execEnv, bool wasm64)
    {
        Id = id;
        Module = module;
        Instance = instance;
        ExecEnv = execEnv;
        Wasm64 = wasm64;
    }

    public void Dispose()
    {
        if (ExecEnv != IntPtr.Zero)
            Libiwasm.wasm_runtime_destroy_exec_env(ExecEnv);

        if (Instance != IntPtr.Zero)
            Libiwasm.wasm_runtime_deinstantiate(Instance);

        if (Module != IntPtr.Zero)
            Libiwasm.wasm_runtime_unload(Module);
    }
}
