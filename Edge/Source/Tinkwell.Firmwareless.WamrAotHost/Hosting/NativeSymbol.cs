using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

[StructLayout(LayoutKind.Sequential)]
struct NativeSymbol
{
    public nint Symbol; // char*
    public nint FuncPtr; // void*
    public nint Signature; // char*
    public nint Attachment; // void*
}
