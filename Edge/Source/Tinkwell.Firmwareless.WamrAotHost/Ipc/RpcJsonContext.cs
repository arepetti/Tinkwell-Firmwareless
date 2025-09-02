using System.Text.Json.Serialization;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(RegisterClientRequest))]
partial class RpcJsonContext : JsonSerializerContext
{
}
