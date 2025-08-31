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
        nint moduleInstance = Libiwasm.wasm_runtime_get_module_inst(execEnv);
        if (moduleInstance == nint.Zero)
            throw new HostException($"Cannot find the module instance for {execEnv}.");

        return moduleInstance;
    }

    public static NativeSymbol MakeNativeSymbol<T>(string name, T del, string signature) where T : notnull, Delegate
    {
        PinnedDelegates.Add(del); // prevents GC
        return new NativeSymbol
        {
            Symbol = StringToHGlobalAnsiPinned(name),
            FuncPtr = Marshal.GetFunctionPointerForDelegate(del),
            Signature = StringToHGlobalAnsiPinned(signature),
            Attachment = IntPtr.Zero
        };
    }

    public static void RegisterNativeFunctions(params NativeSymbol[] syms)
    {
        if (_moduleNamePtr == IntPtr.Zero)
            _moduleNamePtr = StringToHGlobalAnsiPinned("env");

        int size = Marshal.SizeOf<NativeSymbol>() * syms.Length;
        _symbolsPtr = Marshal.AllocHGlobal(size);
        _toFree.Add(_symbolsPtr);

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
        IntPtr err;
        IntPtr module;

        if (modulePath.EndsWith(".aot", StringComparison.OrdinalIgnoreCase))
        {
            module = Libiwasm.wasm_runtime_load_from_aot_file(modulePath, out err, 512);
            if (module == nint.Zero)
                throw new HostException($"Failed to load AOT module '{modulePath}': {ErrorStringPtrToAnsi(err)}");
        }
        else
        {
            var bytes = File.ReadAllBytes(modulePath);
            module = Libiwasm.wasm_runtime_load(bytes, (uint)bytes.Length, out err, 512);
            if (module == nint.Zero)
                throw new HostException($"Failed to load WASM module '{modulePath}': {ErrorStringPtrToAnsi(err)}");
        }

        var inst = Libiwasm.wasm_runtime_instantiate(module, stackSize: 64 * 1024, heapSize: 64 * 1024, out err, 512);
        if (inst == nint.Zero)
            throw new HostException($"Failed to instantiate module '{modulePath}': {ErrorStringPtrToAnsi(err)}");

        var execEnv = Libiwasm.wasm_runtime_create_exec_env(inst, 64 * 1024);
        if (execEnv == nint.Zero)
            throw new HostException("Failed to create execution environment.");

        bool wasm64 = false; // TODO: we need to inspect the module or use the manifest or a CLI parameter

        return new WasmInstance(id, module, inst, execEnv, wasm64)
        {
            OnInitializeFunc = Libiwasm.wasm_runtime_lookup_function(inst, "_initialize", null!),
            OnStartFunc = Libiwasm.wasm_runtime_lookup_function(inst, "_start", null!),
            OnDisposeFunc = Libiwasm.wasm_runtime_lookup_function(inst, "_dispose", null!),
            OnMessageFunc = Libiwasm.wasm_runtime_lookup_function(inst, "_on_message_received", null!),
        };
    }

    public static void Shutdown()
    {
        foreach (var p in _toFree)
        {
            if (p != nint.Zero)
                Marshal.FreeHGlobal(p);
        }

        _toFree.Clear();
        _moduleNamePtr = IntPtr.Zero;
        _symbolsPtr = IntPtr.Zero;

        Libiwasm.wasm_runtime_destroy();
    }

    // Keep delegates alive so their thunks don't get GC'd
    private static readonly List<Delegate> PinnedDelegates = new();
    private static readonly List<IntPtr> _toFree = new();
    private static IntPtr _moduleNamePtr = IntPtr.Zero;
    private static IntPtr _symbolsPtr = IntPtr.Zero;

    private static string GetLastError(WasmInstance inst)
    {
        var errPtr = Libiwasm.wasm_runtime_get_exception(inst.Instance);
        return ErrorStringPtrToAnsi(errPtr);
    }

    private static IntPtr StringToHGlobalAnsiPinned(string s)
    {
        var p = Marshal.StringToHGlobalAnsi(s);
        _toFree.Add(p);
        return p;
    }

    private static string ErrorStringPtrToAnsi(IntPtr ptr)
    {
        if (ptr == nint.Zero)
            return "(no error)";

        return Marshal.PtrToStringAnsi(ptr) ?? "(unknown)";
    }
}