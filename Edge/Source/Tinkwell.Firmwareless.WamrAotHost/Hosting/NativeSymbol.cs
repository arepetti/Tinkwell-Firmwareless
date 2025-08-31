using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

[StructLayout(LayoutKind.Sequential)]
struct NativeSymbol
{
    public IntPtr Symbol;
    public IntPtr FuncPtr; // void*
    public IntPtr Signature;
    public IntPtr Attachment; // void*
}
