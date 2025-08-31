using System.Text;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static class WasmMemory
{
    public static string PtrToStringUtf8(nint moduleInstance, nint ptr, int length)
    {
        if (!Libiwasm.wasm_runtime_validate_app_addr(moduleInstance, ptr, (uint)length))
            throw new HostException($"Invalid memory offset {ptr} (size {length} in module {moduleInstance}.");

        var nativePtr = Libiwasm.wasm_runtime_addr_app_to_native(moduleInstance, ptr);
        if (nativePtr == nint.Zero)
            throw new HostException($"Cannot map memory offset {ptr} (size {length} in module {moduleInstance} to the host.");

        return NativeMemory.PtrToStringUtf8(nativePtr, length);
    }

    public static (nint Ptr, int Length) CopyStringIntoModuleMemory(WasmInstance inst, string text)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);
        nint ptr = Alloc(inst, utf8.Length);
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
        var wasmPtr = Libiwasm.wasm_runtime_module_malloc(inst.Instance, (uint)size, out nint _);
        if (wasmPtr == IntPtr.Zero)
            throw new OutOfMemoryException("WASM malloc failed");

        return wasmPtr;
    }

    public static void Free(WasmInstance inst, nint wasmPtr)
        => Libiwasm.wasm_runtime_module_free(inst.Instance, wasmPtr);
}
