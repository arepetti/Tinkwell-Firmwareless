using System.Text.Json.Serialization;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost.Ipc;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(RegisterClientRequest))]
[JsonSerializable(typeof(MqttMessage))]
partial class RpcJsonContext : JsonSerializerContext
{
}
