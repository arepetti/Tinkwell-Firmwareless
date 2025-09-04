namespace Tinkwell.Firmwareless.WamrAotHost;

static class CancellationTokenExtensions
{
    public static Task WaitForCancellationAsync(this CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(static x => ((TaskCompletionSource<object?>)x!).SetResult(null), tcs);
        return tcs.Task;
    }
}