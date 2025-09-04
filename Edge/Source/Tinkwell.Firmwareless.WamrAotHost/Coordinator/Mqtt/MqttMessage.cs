namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;

record MqttMessage(string HostId, string Topic, string Payload)
{
    public override string ToString()
        => $"{Topic} => {HostId}";
}

enum MqttMessgeDirection
{
    Outgoing,
    Incoming
}

sealed record MqttMessageWithDirection(MqttMessgeDirection Direction, string HostId, string Topic, string Payload) : MqttMessage(HostId, Topic, Payload)
{
    public override string ToString()
        => $"{Direction}: {Topic}";
}
