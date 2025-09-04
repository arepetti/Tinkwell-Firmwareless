using StreamJsonRpc;
using System.Text.Json;

namespace Tinkwell.Firmwareless.WamrAotHost.Ipc;

abstract class IpcBase
{
    protected JsonRpc CreateJsonRpc(Stream stream, object callbacks)
    {
        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = RpcJsonContext.Default,
            }
        };

        var messageHandler = new HeaderDelimitedMessageHandler(stream, stream, formatter);
        var rpc = new JsonRpc(messageHandler);
        rpc.AddLocalRpcTarget(callbacks);

        return rpc;
    }
}
