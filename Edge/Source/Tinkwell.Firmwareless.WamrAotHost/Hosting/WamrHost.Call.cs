using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static partial class WamrHost
{
    public static void CallExportVV(WasmInstance inst, IntPtr func)
    {
        if (func == IntPtr.Zero)
            return;

        if (!Libiwasm.wasm_runtime_call_wasm(inst.ExecEnv, func, 0, IntPtr.Zero))
            throw new HostException($"Error calling (v)v WASM function: {GetLastError(inst)}");
    }

    //public static void CallExportPIPIV(WasmInstance inst, IntPtr func, nint a, int b, nint c, int d)
    //{
    //    if (func == IntPtr.Zero)
    //        return;

    //    int argc = 2;
    //    int size = IntPtr.Size + sizeof(int);
    //    IntPtr argv = Marshal.AllocHGlobal(size);
    //    try
    //    {
    //        unsafe
    //        {
    //            byte* p = (byte*)argv.ToPointer();
    //            *(nint*)p = a;
    //            *(int*)(p + sizeof(nint)) = b;
    //            *(nint*)(p + sizeof(nint) + sizeof(int)) = c;
    //            *(int*)(p + sizeof(nint) + sizeof(int) + sizeof(nint)) = d;
    //        }

    //        if (!Libiwasm.wasm_runtime_call_wasm(inst.ExecEnv, func, (uint)argc, argv))
    //            Console.WriteLine("[HOST] wasm call failed (check your export signature and host build).");
    //    }
    //    finally
    //    {
    //        Marshal.FreeHGlobal(argv);
    //    }
    //}

    public static void CallExportSV(WasmInstance inst, IntPtr func, string text)
    {
        if (func == IntPtr.Zero)
            return;

        var (ptr, len) = WasmMemory.CopyStringIntoModuleMemory(inst, text);
        int ptrSize = inst.Wasm64 ? IntPtr.Size : sizeof(int);

        int argc = 2;
        int size = IntPtr.Size + sizeof(int);
        IntPtr argv = Marshal.AllocHGlobal(size);
        try
        {
            unsafe
            {
                byte* p = (byte*)argv.ToPointer();
                if (inst.Wasm64)
                    *(nint*)p = ptr;
                else
                    *(int*)p = (int)ptr;
                *(int*)(p + ptrSize) = len;
            }

            if (!Libiwasm.wasm_runtime_call_wasm(inst.ExecEnv, func, (uint)argc, argv))
                throw new HostException($"Error calling (pi)v WASM function: {GetLastError(inst)}");
        }
        finally
        {
            Marshal.FreeHGlobal(argv);
            WasmMemory.Free(inst, ptr);
        }
    }
}
