namespace Tinkwell.Firmwareless.WasmHost
{
    interface IPublicRepository
    {
        Task<string> GetPublicKeyAsync(CancellationToken cancellationToken);
    }
}