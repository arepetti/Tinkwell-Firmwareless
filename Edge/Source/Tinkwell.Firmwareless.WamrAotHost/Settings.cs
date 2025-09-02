namespace Tinkwell.Firmwareless.WamrAotHost;

public sealed class Settings
{
    public int CoordinatorStartProcessTimeoutMs { get; set; } = 30_000;
    public int CoordinatorMaxAllowedServerInstances { get; set; } = -1;
    public int HostConnectionTimeout { get; set; } = 5_000;
    public int HostMaxConnectionAttempts { get; set; } = 5;
    public int HostDelayBetweenAttemptsMs { get; set; } = 1_000;
}
