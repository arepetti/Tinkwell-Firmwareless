namespace Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

sealed class RegisterClientRequest
{
    public required string ClientName { get; set; }
}
