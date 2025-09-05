namespace Tinkwell.Firmwareless.WamrAotHost;

static class CancellationTokenExtensions
{
    public static async Task WaitCancellation(this CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Expected when stopping
        }
    }
}