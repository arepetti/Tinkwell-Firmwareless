namespace Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

sealed class RegisterClientRequest
{
    public required string ClientName { get; set; }
}

static class CoordinatorMethods
{
    public const string RegisterClient = "RegisterClient";
}

static class HostMethods
{
    public const string Shutdown = "Shutdown";
}