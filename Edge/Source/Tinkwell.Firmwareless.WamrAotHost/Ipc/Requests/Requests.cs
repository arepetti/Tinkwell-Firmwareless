using Tinkwell.Firmwareless.WamrAotHost.Coordinator;

namespace Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

sealed class RegisterClientRequest
{
    public required string ClientName { get; set; }
}

static class CoordinatorMethods
{
    public const string RegisterClient = nameof(CoordinatorRpc.RegisterClient);
    public const string PublishMqttMessage = nameof(CoordinatorRpc.PublishMqttMessage);
}

static class HostMethods
{
    public const string Shutdown = nameof(HostService.Shutdown);
    public const string ReceiveMqttMessage = nameof(HostService.ReceiveMqttMessage);
}