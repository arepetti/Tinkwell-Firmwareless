using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static class Libiwasm
{
    private const string Lib = "libiwasm";

    public enum WamrLogLevel
    {
        Fatal = 0,
        Error = 1,
        Warning = 2,
        Debug = 3,
        Verbose = 4
    }

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr wasm_runtime_get_exception(IntPtr module_inst);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool wasm_runtime_init();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void wasm_runtime_destroy();

    [DllImport("libiwasm", CallingConvention = CallingConvention.Cdecl)]
    public static extern void wasm_runtime_set_log_level(WamrLogLevel level);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr wasm_runtime_load(byte[] buf, uint size, out IntPtr errorBuf, uint errorBufSize);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr wasm_runtime_load_from_aot_file(string path, out IntPtr errorBuf, uint errorBufSize);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr wasm_runtime_instantiate(IntPtr module, uint stackSize, uint heapSize, out IntPtr errorBuf, uint errorBufSize);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void wasm_runtime_deinstantiate(IntPtr moduleInst);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void wasm_runtime_unload(IntPtr module);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr wasm_runtime_create_exec_env(IntPtr moduleInst, uint stackSize);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void wasm_runtime_destroy_exec_env(IntPtr execEnv);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool wasm_runtime_call_wasm(IntPtr execEnv, IntPtr function, uint argc, IntPtr argv);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr wasm_runtime_lookup_function(IntPtr moduleInst,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        [MarshalAs(UnmanagedType.LPStr)] string? signature);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr wasm_runtime_get_module_inst(IntPtr execEnv);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool wasm_runtime_validate_app_addr(IntPtr moduleInst, nint appOffset, uint size);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr wasm_runtime_addr_app_to_native(IntPtr moduleInst, nint appOffset);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool wasm_runtime_register_natives(
        IntPtr moduleName,
        IntPtr symbols,
        uint nSymbols);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint wasm_runtime_get_memory_size(IntPtr moduleInst);
    
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr wasm_runtime_module_malloc(IntPtr moduleInst, uint size, out IntPtr nativeAddr);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void wasm_runtime_module_free(IntPtr moduleInst, IntPtr ptr);
}
