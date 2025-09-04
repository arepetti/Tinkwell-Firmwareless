using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator.Monitoring;

// This is going to be removed when Microsoft.Extensions.Diagnostics.ResourceMonitoring will support
// all the metrics we will need (total memory, system memory usage %, system CPU usage %). We're going
// to probably support OpenTelemetry to give a chance to our parent (WasmHost) to control what's going on
// inside the container.
static class PlatformUtils
{
    public static ulong? GetTotalPhysicalMemory()
    {
        if (_totalPhysicalMemoryCalculated)
            return _totalPhysicalMemory;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return _totalPhysicalMemory = GetWindowsTotalMemory();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return _totalPhysicalMemory = GetLinuxTotalMemory();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return _totalPhysicalMemory = GetOsXTotalMemory();
        }
        catch
        {
            // Ignore errors
        }
        finally
        {
            _totalPhysicalMemoryCalculated = true;
        }

        return null;
    }

    private static bool _totalPhysicalMemoryCalculated;
    private static ulong? _totalPhysicalMemory;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    private static ulong? GetWindowsTotalMemory()
    {
        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(memStatus))
            return memStatus.ullTotalPhys;

        return null;
    }

    private static ulong? GetLinuxTotalMemory()
    {
        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            if (line.StartsWith("MemTotal:"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (ulong.TryParse(parts[1], out ulong kb))
                    return kb * 1024;

                return null;
            }
        }

        return null;
    }

    private static ulong? GetOsXTotalMemory()
    {
        var psi = new ProcessStartInfo("sysctl", "-n hw.memsize")
        {
            RedirectStandardOutput = true
        };

        using var proc = Process.Start(psi);
        string output = proc!.StandardOutput.ReadToEnd();
        if (ulong.TryParse(output.Trim(), out ulong bytes))
            return bytes;

        return null;
    }
}