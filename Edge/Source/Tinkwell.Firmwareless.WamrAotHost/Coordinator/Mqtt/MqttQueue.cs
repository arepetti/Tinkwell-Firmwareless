using System.Collections.Concurrent;
using System.Diagnostics;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;

sealed class MqttQueue : IMqttQueue
{
    public void EnqueueIncomingMessage(MqttMessage message)
    {
        _items.Enqueue(new(MqttMessgeDirection.Incoming, message.HostId, message.Topic, message.Payload));
        _signal.Release();
    }

    public void EnqueueOutgoingMessage(MqttMessage message)
    {
        _items.Enqueue(new(MqttMessgeDirection.Outgoing, message.HostId, message.Topic, message.Payload));
        _signal.Release();
    }

    public async Task<MqttMessageWithDirection> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        bool success = _items.TryDequeue(out var message);
        Debug.Assert(success && message is not null);
        return message;
    }

    private readonly ConcurrentQueue<MqttMessageWithDirection> _items = new();
    private readonly SemaphoreSlim _signal = new(0);
}
