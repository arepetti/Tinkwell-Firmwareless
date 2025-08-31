using System.Runtime.InteropServices;
using System.Text;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static class NativeMemory
{
    public static string PtrToStringUtf8(IntPtr ptr, int len)
    {
        if (len <= 0)
            return string.Empty;

        byte[] bytes = new byte[len];
        Marshal.Copy(ptr, bytes, 0, len);

        int realLen = Array.IndexOf<byte>(bytes, 0);
        if (realLen < 0)
            realLen = len;

        return Encoding.UTF8.GetString(bytes, 0, realLen);
    }
}
