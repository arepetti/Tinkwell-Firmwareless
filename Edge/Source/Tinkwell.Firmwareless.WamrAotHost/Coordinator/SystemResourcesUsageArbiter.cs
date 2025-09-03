namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

sealed class SystemResourcesUsageArbiter(Settings settings)
{
    public enum Decision
    {
        None,
        Suspend,
        Terminate
    }

    public IEnumerable<(Decision Decision, HostInfo HostInfo)> Assess(IEnumerable<HostInfo> hosts)
    {
        foreach (var host in hosts)
        {
            if (host.Process is null || host.Process.HasExited)
                yield return (Decision.None, host);

            if (host.Ready == false || host.Terminating)
                yield return (Decision.None, host);

            if (host.UsageData.CpuUsagePercentage.ExponentialMovingAverage(0.65) > _settings.CoordinatorMaxHostCpuUsagePercentage)
                yield return(Decision.Terminate, host);

            if (host.UsageData.MemoryUsagePercentage.ExponentialMovingAverage(0.75) > _settings.CoordinatorMaxHostMemoryUsagePercentage)
                yield return (Decision.Terminate, host);
        }

        // TODO: check if the system is under heavy load and suspend hosts accordingly
        // (oldest one first or by resource usage or by activity - if we can get that anywhere, maybe tracking messages)
    }

    private readonly Settings _settings = settings;
}