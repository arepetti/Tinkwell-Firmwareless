using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Server;
using System.Net;

namespace Tinkwell.Firmwareless.Tools.MqttBroker;

public sealed class MqttBroker(ILogger<MqttBroker> logger) : BackgroundService
{
    public const int Port = 1883;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Starting MQTT broker on port {Port}...", Port);

        var serverOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointBoundIPAddress(IPAddress.Any)
            .WithDefaultEndpointPort(Port)
            .Build();

        var mqttFactory = new MqttServerFactory();
        using var mqttServer = mqttFactory.CreateMqttServer(serverOptions);

        mqttServer.ClientConnectedAsync += e =>
        {
            _logger.LogInformation("Client '{ClientId}' connected.", e.ClientId);
            return Task.CompletedTask;
        };

        mqttServer.ClientDisconnectedAsync += e =>
        {
            _logger.LogInformation("Client '{ClientId}' disconnected.", e.ClientId);
            return Task.CompletedTask;
        };

        mqttServer.ApplicationMessageEnqueuedOrDroppedAsync += e =>
        {
            _logger.LogInformation("Sender {SenderName} sent a {Status} {Topic} with {Payload}",
                e.SenderClientId,
                e.IsDropped ? "DROPPED message" : "message",
                e.ApplicationMessage.Topic,
                e.ApplicationMessage.ConvertPayloadToString()
            );
            return Task.CompletedTask;
        };

        await mqttServer.StartAsync();
        _logger.LogInformation("MQTT broker started successfully on port {Port}.", Port);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
        }

        _logger.LogInformation("Stopping MQTT broker...");
        await mqttServer.StopAsync();
    }

    private readonly ILogger<MqttBroker> _logger = logger;
}
