using System.Diagnostics;
using System.Text;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static class WasmMemory
{
    public static nint MapAppAddressToNative(nint moduleInstance, nint ptr)
    {
        Debug.Assert(moduleInstance != nint.Zero);
        Debug.Assert(ptr != nint.Zero);

        var nativePtr = Libiwasm.wasm_runtime_addr_app_to_native(moduleInstance, ptr);
        if (nativePtr == nint.Zero)
            throw new HostException($"Cannot map memory offset {ptr} in module {moduleInstance} to the host.");

        return nativePtr;
    }

    public static nint MapAppAddressRangeToNative(nint moduleInstance, nint ptr, int length)
    {
        Debug.Assert(moduleInstance != nint.Zero);
        Debug.Assert(ptr != nint.Zero);
        Debug.Assert(length > 0);

        var nativePtr = MapAppAddressToNative(moduleInstance, ptr);
        if (!Libiwasm.wasm_runtime_validate_app_addr(moduleInstance, ptr, (uint)length))
            throw new HostException($"Invalid memory offset {ptr} (size {length}) in module {moduleInstance}.");

        return nativePtr;
    }

    public static string Utf8PtrToString(nint moduleInstance, nint ptr, int length)
    {
        var nativePtr = MapAppAddressRangeToNative(moduleInstance, ptr, length);
        return NativeMemory.Utf8PtrToString(nativePtr, length);
    }

    public static string HighlyUnsafeUtf8PtrToString(nint moduleInstance, nint ptr)
    {
        Debug.Assert(moduleInstance != nint.Zero);
        Debug.Assert(ptr != nint.Zero);

        // We set an arbitrary maximum. The code using this function should be trusted because this should be used only by
        // wasm compiler generated code but a malicious firmware could break it. With this limit we basically impose that
        // the maxium message length is 512 bytes and that the string address (even if it's shorter!) cannot be in the last 512 bytes
        // of any 64K memory section. Use with caution!
        const int maximumLength = 512;
        if (!Libiwasm.wasm_runtime_validate_app_addr(moduleInstance, ptr, maximumLength))
            throw new HostException($"Invalid memory offset {ptr} (unknown size) in module {moduleInstance}.");

        var nativePtr = Libiwasm.wasm_runtime_addr_app_to_native(moduleInstance, ptr);
        if (nativePtr == nint.Zero)
            throw new HostException($"Cannot map memory offset {ptr} (unknown size) in module {moduleInstance} to the host.");

        int length = 0;
        unsafe
        {
            byte* p = (byte*)ptr;
            while (*p++ != 0 && length < maximumLength)
                ++length;
        }

        if (length == 0)
            return "";

        return NativeMemory.Utf8PtrToString(nativePtr, length);
    }

    public static (nint Ptr, int Length) CopyStringIntoModuleMemoryAsUtf8(WasmInstance inst, string text)
    {
        Debug.Assert(inst.Instance != nint.Zero);

        var utf8 = Encoding.UTF8.GetBytes(text);
        var ptr = Alloc(inst, utf8.Length);
        unsafe
        {
            nint dest = Libiwasm.wasm_runtime_addr_app_to_native(inst.Instance, ptr);
            fixed (byte* src = utf8)
            {
                Buffer.MemoryCopy(src, (void*)dest, utf8.Length, utf8.Length);
            }
        }

        return (ptr, utf8.Length);
    }

    public static nint Alloc(WasmInstance inst, int size)
    {
        Debug.Assert(inst.Instance != nint.Zero);
        Debug.Assert(size > 0);

        var wasmPtr = Libiwasm.wasm_runtime_module_malloc(inst.Instance, (uint)size, out nint _);
        if (wasmPtr == nint.Zero)
            throw new HostException("WASM malloc failed: out of memory");

        return wasmPtr;
    }

    public static void Free(WasmInstance inst, nint wasmPtr)
    {
        Debug.Assert(inst.Instance != nint.Zero);
        Debug.Assert(wasmPtr != nint.Zero);

        Libiwasm.wasm_runtime_module_free(inst.Instance, wasmPtr);
    }
}
