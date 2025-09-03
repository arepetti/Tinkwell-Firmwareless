namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

static class ResourcesUsageMeter
{
    public static async Task CollectAsync(IEnumerable<HostInfo> hosts)
    {
        Dictionary<string, CpuUsage> cpuStats = new();
        foreach (var hostInfo in hosts)
        {
            if (hostInfo.Process is null || hostInfo.Process.HasExited)
                continue;

            cpuStats.Add(hostInfo.Id, new CpuUsage(DateTime.UtcNow, hostInfo.Process.TotalProcessorTime));
        }

        await Task.Delay(1000);

        foreach (var hostInfo in hosts)
        {
            if (hostInfo.Process is null || hostInfo.Process.HasExited)
                continue;

            if (!cpuStats.TryGetValue(hostInfo.Id, out var stats))
                continue; // New process

            hostInfo.Process.Refresh();

            hostInfo.UsageData.CpuUsagePercentage.Push(
                CalculateCpuUsage(DateTime.UtcNow - stats.Timestamp, stats.TotalProcessorTime, hostInfo.Process.TotalProcessorTime));

            if (_totalPhysicalMemory is not null)
                hostInfo.UsageData.MemoryUsagePercentage.Push(CalculateMemoryUsage(hostInfo.Process.WorkingSet64));
        }
    }

    private static double CalculateCpuUsage(TimeSpan interval, TimeSpan firstProcessorTime, TimeSpan secondProcessorTime)
    {
        double cpuUsed = (secondProcessorTime - firstProcessorTime).TotalMilliseconds;
        double cpuUsageTotal = cpuUsed / (interval.TotalMilliseconds * Environment.ProcessorCount) * 100;
        return Math.Clamp(Math.Round(cpuUsageTotal, 1), 0, 100);
    }

    private static double CalculateMemoryUsage(long workingSet)
        => Math.Round(Math.Clamp((double)workingSet / _totalPhysicalMemory!.Value, 0, 100), 1); // Precision for long->double isn't an issue here

    private record CpuUsage(DateTime Timestamp, TimeSpan TotalProcessorTime);

    private readonly static ulong? _totalPhysicalMemory = PlatformUtils.GetTotalPhysicalMemory();
}
