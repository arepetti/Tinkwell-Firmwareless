using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

sealed class CoordinatorRpc(ILogger<CoordinatorRpc> logger, FirmletsRepository repository, IMqttQueue messageQueue)
{
    [JsonRpcMethod(CoordinatorMethods.RegisterClient)]
    public void RegisterClient(RegisterClientRequest request)
    {
        if (_repository.TryGetByHostId(request.ClientName, out var host))
        {
            _logger.LogInformation("Host {HostId} is ready ({Time} ms)",
                request.ClientName, (DateTime.UtcNow - host.StartTime).TotalMilliseconds);

            host.Ready = true;
        }
    }

    [JsonRpcMethod(CoordinatorMethods.PublishMqttMessage)]
    public void PublishMqttMessage(MqttMessage request)
    {
        _logger.LogTrace("Received message from {HostId}: {Topic}", request.HostId, request.Topic)
;        if (!_repository.TryGetByHostId(request.HostId, out var _))
            _logger.LogError("Received a request to send an MQTT nessage from an unknown sender {HostId}", request.HostId);
        else
            _messageQueue.EnqueueOutgoingMessage(request);
    }

    private readonly ILogger<CoordinatorRpc> _logger = logger;
    private readonly FirmletsRepository _repository = repository;
    private readonly IMqttQueue _messageQueue = messageQueue;
}
