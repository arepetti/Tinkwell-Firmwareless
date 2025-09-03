namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

sealed record HostInfoResourceUsageData(FixedSizeValueList CpuUsagePercentage, FixedSizeValueList MemoryUsagePercentage)
{
    public void Clear()
    {
        CpuUsagePercentage.Clear();
        MemoryUsagePercentage.Clear();
    }
}
