using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static partial class Wamr
{
    public static string Signature(Type returnType, params Type[] paramTypes)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('(');
        foreach (var pt in paramTypes)
            sb.Append(TypeChar(pt));
        sb.Append(')');

        if (returnType != typeof(void))
            sb.Append(TypeChar(returnType));

        return sb.ToString();
    }

    public static void CallExportVV(WasmInstance inst, nint func, bool required = false)
    {
        Debug.Assert(inst.ExecEnv != nint.Zero);

        if (IsCallable(inst, func, required) == false)
            return;

        if (!Libiwasm.wasm_runtime_call_wasm(inst.ExecEnv, func, 0, nint.Zero))
            throw new HostException($"Error calling (v)v WASM function: {GetLastError(inst)}");
    }

    public static void CallExportIV(WasmInstance inst, nint func, int arg, bool required = false)
    {
        Debug.Assert(inst.ExecEnv != nint.Zero);

        if (IsCallable(inst, func, required) == false)
            return;

        int argc = 1;
        int size = sizeof(int);
        nint argv = Marshal.AllocHGlobal(size);
        try
        {
            unsafe
            {
                byte* p = (byte*)argv.ToPointer();
                *(int*)p = arg;
            }

            if (!Libiwasm.wasm_runtime_call_wasm(inst.ExecEnv, func, (uint)argc, argv))
                throw new HostException($"Error calling (i)v WASM function: {GetLastError(inst)}");
        }
        finally
        {
            Marshal.FreeHGlobal(argv);
        }
    }

    public static void CallExportSV(WasmInstance inst, nint func, string text, bool required = false)
    {
        Debug.Assert(inst.ExecEnv != nint.Zero);

        if (IsCallable(inst, func, required) == false)
            return;

        var (ptr, len) = WasmMemory.CopyStringIntoModuleMemoryAsUtf8(inst, text);
        int ptrSize = inst.Wasm64 ? nint.Size : sizeof(int);

        int argc = 2;
        int size = ptrSize + sizeof(int);
        nint argv = Marshal.AllocHGlobal(size);
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



    public static void CallExportSSV(WasmInstance inst, nint func, string text1, string text2, bool required = false)
    {
        Debug.Assert(inst.ExecEnv != nint.Zero);

        if (IsCallable(inst, func, required) == false)
            return;

        // TODO: REUSE THIS MEMORY!!! This function is probably called MANY many times: keep a buffer
        // around instead of allocating/freeing each time.
        var (ptr1, len1) = WasmMemory.CopyStringIntoModuleMemoryAsUtf8(inst, text1);
        var (ptr2, len2) = WasmMemory.CopyStringIntoModuleMemoryAsUtf8(inst, text1);
        int ptrSize = inst.Wasm64 ? nint.Size : sizeof(int);

        int argc = 4;
        int size = 2 * (ptrSize + sizeof(int));
        nint argv = Marshal.AllocHGlobal(size);
        try
        {
            unsafe
            {
                byte* p = (byte*)argv.ToPointer();

                if (inst.Wasm64)
                    *(nint*)p = ptr1;
                else
                    *(int*)p = (int)ptr1;
                *(int*)(p + ptrSize) = len1;

                if (inst.Wasm64)
                    *(nint*)(p + ptrSize + sizeof(int)) = ptr2;
                else
                    *(int*)(p + ptrSize + sizeof(int)) = (int)ptr2;
                *(int*)(p + ptrSize + sizeof(int) + ptrSize) = len2;
            }

            if (!Libiwasm.wasm_runtime_call_wasm(inst.ExecEnv, func, (uint)argc, argv))
                throw new HostException($"Error calling (pipi)v WASM function: {GetLastError(inst)}");
        }
        finally
        {
            Marshal.FreeHGlobal(argv);
            WasmMemory.Free(inst, ptr1);
            WasmMemory.Free(inst, ptr2);
        }
    }

    private static char TypeChar(Type t)
        => t switch
        {
            var _ when t == typeof(int) => 'i',
            var _ when t == typeof(long) => 'l',
            var _ when t == typeof(float) => 'f',
            var _ when t == typeof(double) => 'd',
            var _ when t == typeof(nint) => 'i', // TODO: if the module is wasm64 then this is "l"
            var _ when t == typeof(void) => 'v',
            _ => throw new NotSupportedException($"Type '{t.FullName}' is not supported in WAMR signatures.")
        };

    private static bool IsCallable(WasmInstance inst, nint func, bool required)
    {
        if (func != nint.Zero)
            return true;

        if (required)
            throw new HostException($"Module {inst.Id} does not export a required function (or function signature mismatch).");

        return false;
    }
}
