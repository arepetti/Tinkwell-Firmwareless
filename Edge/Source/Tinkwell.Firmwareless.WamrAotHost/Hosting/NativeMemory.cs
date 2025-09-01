using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static class NativeMemory
{
    public static string Utf8PtrToString(nint ptr, int len)
    {
        if (ptr == nint.Zero || len <= 0)
            return "";

        byte[] bytes = new byte[len];
        Marshal.Copy(ptr, bytes, 0, len);

        int realLen = Array.IndexOf<byte>(bytes, 0);
        if (realLen < 0)
            realLen = len;

        return Encoding.UTF8.GetString(bytes, 0, realLen);
    }

    public static string AnsiPtrToString(nint ptr)
    {
        if (ptr == nint.Zero)
            return "";

        return Marshal.PtrToStringAnsi(ptr) ?? "";
    }

    public static nint StringToHGlobalAnsi(string s)
    {
        var p = Marshal.StringToHGlobalAnsi(s);
        _toFree.Add(p);
        return p;
    }

    public static nint Alloc(int size)
    {
        Debug.Assert(size > 0);

        nint ptr = Marshal.AllocHGlobal(size);
        _toFree.Add(ptr);
        return ptr;
    }

    public static void FreeAll()
    {
        foreach (var p in _toFree)
        {
            if (p != nint.Zero)
                Marshal.FreeHGlobal(p);
        }

        _toFree.Clear();
    }

    public static void Pin(Delegate del)
        => _pinnedDelegates.Add(del);

    // Keep delegates alive so their thunks don't get GC'd
    private static readonly List<Delegate> _pinnedDelegates = new();
    private static readonly List<IntPtr> _toFree = new();
}
