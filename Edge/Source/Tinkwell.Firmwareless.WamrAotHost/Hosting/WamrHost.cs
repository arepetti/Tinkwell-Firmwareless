using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static partial class WamrHost
{
    public static void Initialize()
    {
        if (!Libiwasm.wasm_runtime_init())
            throw new HostException("Failed to init WAMR.");

        Libiwasm.wasm_runtime_set_log_level(Libiwasm.WamrLogLevel.Warning);
    }

    public static nint GetModuleInstanceFromExecEnvHandle(nint execEnv)
    {
        Debug.Assert(execEnv != nint.Zero);

        nint moduleInstance = Libiwasm.wasm_runtime_get_module_inst(execEnv);
        if (moduleInstance == nint.Zero)
            throw new HostException($"Cannot find the module instance for {execEnv}.");

        return moduleInstance;
    }

    public static NativeSymbol MakeNativeSymbol<T>(string name, T del, string signature) where T : notnull, Delegate
    {
        NativeMemory.Pin(del);
        return new NativeSymbol
        {
            Symbol = NativeMemory.StringToHGlobalAnsi(name),
            FuncPtr = Marshal.GetFunctionPointerForDelegate(del),
            Signature = NativeMemory.StringToHGlobalAnsi(signature),
            Attachment = IntPtr.Zero
        };
    }

    public static void RegisterNativeFunctions(params NativeSymbol[] syms)
    {
        if (_moduleNamePtr == IntPtr.Zero)
            _moduleNamePtr = NativeMemory.StringToHGlobalAnsi("env");

        int size = Marshal.SizeOf<NativeSymbol>() * syms.Length;
        _symbolsPtr = NativeMemory.Alloc(size);

        nint cur = _symbolsPtr;
        foreach (var s in syms)
        {
            Marshal.StructureToPtr(s, cur, false);
            cur += Marshal.SizeOf<NativeSymbol>();
        }

        if (!Libiwasm.wasm_runtime_register_natives(_moduleNamePtr, _symbolsPtr, (uint)syms.Length))
            throw new HostException("Failed to register native functions.");
    }

    public static WasmInstance LoadInstance(string id, string modulePath)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(id));
        Debug.Assert(!string.IsNullOrWhiteSpace(modulePath));

        IntPtr errorMessage;
        IntPtr moduleInstance;

        if (modulePath.EndsWith(".aot", StringComparison.OrdinalIgnoreCase))
        {
            moduleInstance = Libiwasm.wasm_runtime_load_from_aot_file(modulePath, out errorMessage, 512);
            if (moduleInstance == nint.Zero)
                throw new HostException($"Failed to load AOT module '{modulePath}': {NativeMemory.AnsiPtrToString(errorMessage)}");
        }
        else
        {
            var bytes = File.ReadAllBytes(modulePath);
            moduleInstance = Libiwasm.wasm_runtime_load(bytes, (uint)bytes.Length, out errorMessage, 512);
            if (moduleInstance == nint.Zero)
                throw new HostException($"Failed to load WASM module '{modulePath}': {NativeMemory.AnsiPtrToString(errorMessage)}");
        }

        var inst = Libiwasm.wasm_runtime_instantiate(moduleInstance, stackSize: 64 * 1024, heapSize: 64 * 1024, out errorMessage, 512);
        if (inst == nint.Zero)
            throw new HostException($"Failed to instantiate module '{modulePath}': {NativeMemory.AnsiPtrToString(errorMessage)}");

        var execEnv = Libiwasm.wasm_runtime_create_exec_env(inst, 64 * 1024);
        if (execEnv == nint.Zero)
            throw new HostException("Failed to create execution environment.");

        bool wasm64 = false; // TODO: we need to inspect the moduleInstance or use the manifest or a CLI parameter

        return new WasmInstance(id, moduleInstance, inst, execEnv, wasm64)
        {
            OnInitializeFunc = Libiwasm.wasm_runtime_lookup_function(inst, "_initialize", Signature(typeof(void), typeof(nint), typeof(int))),
            OnStartFunc = Libiwasm.wasm_runtime_lookup_function(inst, "_start", Signature(typeof(void), typeof(void))),
            OnDisposeFunc = Libiwasm.wasm_runtime_lookup_function(inst, "_dispose", Signature(typeof(void), typeof(void))),
            OnMessageFunc = Libiwasm.wasm_runtime_lookup_function(inst, "_on_message_received", Signature(typeof(void), typeof(nint), typeof(int), typeof(nint), typeof(int))),
        };
    }

    public static void Shutdown()
    {
        NativeMemory.FreeAll();
        _moduleNamePtr = IntPtr.Zero;
        _symbolsPtr = IntPtr.Zero;

        Libiwasm.wasm_runtime_destroy();
    }

    private static IntPtr _moduleNamePtr = IntPtr.Zero;
    private static IntPtr _symbolsPtr = IntPtr.Zero;

    private static string GetLastError(WasmInstance inst)
    {
        Debug.Assert(inst.Instance != nint.Zero);

        var errPtr = Libiwasm.wasm_runtime_get_exception(inst.Instance);
        return NativeMemory.AnsiPtrToString(errPtr);
    }
}