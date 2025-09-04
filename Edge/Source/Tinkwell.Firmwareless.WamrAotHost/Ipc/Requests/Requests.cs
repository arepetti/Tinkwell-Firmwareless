namespace Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

sealed class RegisterClientRequest
{
    public required string ClientName { get; set; }
}

sealed record PublishMqttMessageRequest(string HostId, string Topic, string Payload);

static class CoordinatorMethods
{
    public const string RegisterClient = "RegisterClient";
    public const string PublishMqttMessage = "PublishMqttMessage";
}

static class HostMethods
{
    public const string Shutdown = "Shutdown";
    public const string ReceiveMqttMessage = "ReceiveMqttMessage";
}