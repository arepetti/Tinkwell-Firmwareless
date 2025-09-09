namespace Tinkwell.Firmwareless.WasmHost;

sealed class Settings
{
    public string ImageName { get; set; } = "wamraothost";
    public string ImageTag { get; set; } = "1.0.0";
    public string ImageTarFileName { get; set; } = "wamraothost-1.0.0.tar";
    public string MqttBrokerAddress { get; set; } = "host.docker.internal";
    public int MqttBrokerPort { get; set; } = 1883;
    public int ShutdownTimeoutSeconds { get; set; } = 30;
}