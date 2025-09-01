namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class WasmInstance : IDisposable
{
    public readonly string Id;
    public readonly nint Module;
    public readonly nint Instance;
    public readonly nint ExecEnv;
    public readonly bool Wasm64;
    public required nint OnInitializeFunc;
    public required nint OnStartFunc;
    public required nint OnDisposeFunc;
    public required nint OnMessageFunc;

    public WasmInstance(string id, IntPtr module, nint instance, nint execEnv, bool wasm64)
    {
        Id = id;
        Module = module;
        Instance = instance;
        ExecEnv = execEnv;
        Wasm64 = wasm64;
    }

    public void Dispose()
    {
        if (ExecEnv != nint.Zero)
            Libiwasm.wasm_runtime_destroy_exec_env(ExecEnv);

        if (Instance != nint.Zero)
            Libiwasm.wasm_runtime_deinstantiate(Instance);

        if (Module != nint.Zero)
            Libiwasm.wasm_runtime_unload(Module);
    }
}
