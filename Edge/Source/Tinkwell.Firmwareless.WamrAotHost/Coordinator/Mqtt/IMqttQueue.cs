namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;

interface IMqttQueue
{
    void EnqueueOutgoingMessage(MqttMessage message);

    void EnqueueIncomingMessage(MqttMessage message);

    Task<MqttMessageWithDirection> DequeueAsync(CancellationToken cancellationToken);
}
