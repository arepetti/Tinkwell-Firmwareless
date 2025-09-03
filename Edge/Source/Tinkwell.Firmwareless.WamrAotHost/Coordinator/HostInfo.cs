using System.Diagnostics;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

[DebuggerDisplay("{Id}")]
sealed record HostInfo(string Id, string Path)
{
    public bool Ready { get; set; }
    public bool Terminating { get; set; }
    public Process? Process { get; set; }
    public DateTime StartTime { get; set; }
    public List<DateTime> RestartTimestamps { get; } = new();
    public HostInfoResourceUsageData UsageData { get; } = new HostInfoResourceUsageData(new(5), new(5));

    public override string ToString()
    {
        return string.Format("{0}: {1}, {2}. CPU: {3}%, memory {4}%",
            Id,
            Process is null || Process.HasExited ? "terminated" : "active",
            Ready ? "ready" : "booting",
            UsageData.CpuUsagePercentage.Current,
            UsageData.MemoryUsagePercentage.Current
        );
    }
}
