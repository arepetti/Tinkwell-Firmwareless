using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using System.Diagnostics;
using Tinkwell.Firmwareless.WamrAotHost.Ipc;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;

sealed class MqttMessagesProcessingService(
    ILogger<MqttMessagesProcessingService> logger,
    IMqttQueue queue,
    IpcServer ipcServer,
    FirmletsRepository repository,
    Settings settings,
    CoordinatorServiceOptions options
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.MqttBrokerAddress))
        {
            _logger.LogWarning("MQTT broker address not specified, MQTT won't be available.");
        }
        else
        {
            _logger.LogDebug("MQTT message processing service starting...");
            await CreateMqttClientAndConnectAsync(stoppingToken);
        }

        _logger.LogDebug("MQTT message processing service started");
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await _queue.DequeueAsync(stoppingToken);
            try
            {
                if (message.Direction == MqttMessgeDirection.Incoming)
                    await ProcessIncomingMessageAsync(message, stoppingToken);
                else
                    await ProcessOutgoingMessageAsync(message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message {MessageInfo}", message.ToString());
            }
        }

        _logger.LogDebug("MQTT message processing service stopping...");
    }

    private readonly IMqttQueue _queue = queue;
    private readonly ILogger<MqttMessagesProcessingService> _logger = logger;
    private readonly IpcServer _ipcServer = ipcServer;
    private readonly FirmletsRepository _repository = repository;
    private readonly Settings _settings = settings;
    private readonly CoordinatorServiceOptions _options = options;
    private IMqttClient? _mqttClient;
    private MqttClientOptions? _clientOptions;

    private async Task ProcessIncomingMessageAsync(MqttMessage message, CancellationToken cancellationToken)
    {
        if (_repository.TryGetByHostId(message.HostId, out _))
            await _ipcServer.NotifyAsync(message.HostId, HostMethods.ReceiveMqttMessage, new MqttMessage(message.HostId, message.Topic, message.Payload));
        else
            _logger.LogWarning("Cannot find host {HostId} to deliver MQTT nessage {Topic}", message.HostId, message.Topic);
    }

    private async Task ProcessOutgoingMessageAsync(MqttMessage message, CancellationToken cancellationToken)
    {
       if (!_repository.TryGetByHostId(message.HostId, out var host))
        {
            _logger.LogError("A message with an unknown host {HostId} has been queued for delivery (this is an internal error!)", message.HostId);
            return;
        }

        string topic = MqttTopicTranslator.FromPlainToTinkwell(host, message.Topic);
        string payload = message.Payload;

        await PublishAsync(topic, payload, cancellationToken);
    }

    private async Task CreateMqttClientAndConnectAsync(CancellationToken cancellationToken)
    {
        var mqttFactory = new MqttClientFactory();
        _mqttClient = mqttFactory.CreateMqttClient();

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(_options.MqttClientId)
            .WithTcpServer(_options.MqttBrokerAddress, _options.MqttBrokerPort);

        var username = Environment.GetEnvironmentVariable("TW_MQTT_CREDENTIALS_USERNAME");
        var password = Environment.GetEnvironmentVariable("TW_MQTT_CREDENTIALS_PASSWORD");
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            clientOptionsBuilder.WithCredentials(username, password);

        _clientOptions = clientOptionsBuilder.Build();

        _mqttClient.ConnectedAsync += HandleConnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;

        await ConnectAsync(cancellationToken);
        _mqttClient.DisconnectedAsync += HandleClientDissconnectedAsync;
    }

    private async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (_mqttClient is null)
            throw new InvalidOperationException("MQTT client is not initialized. Please start the bridge first.");

        _logger.LogTrace("Publishing MQTT message {Topic}...", topic);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        // TODO: add retry logic here!
        var result = await _mqttClient.PublishAsync(message, cancellationToken);

        switch (result.ReasonCode)
        {
            case MqttClientPublishReasonCode.Success:
                _logger.LogTrace("Successfully sent MQTT message {Topic}...", topic);
                break;
            case MqttClientPublishReasonCode.NoMatchingSubscribers:
                _logger.LogTrace("MQTT message with topic {Topic} had no receiver", topic);
                break;
            default:
                _logger.LogError("MQTT message delivery error {ErrorCode}: {ErrorMessage}", result.ReasonCode, result.ReasonString);
                break;
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_mqttClient is not null);
        Debug.Assert(_clientOptions is not null);

        for (int i = 0; i < _settings.MqttMaxRetries; ++i)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            _logger.LogDebug("Connecting to MQTT broker {Address}:{Port} (attempt {Attempt}/{TotalAttempts})...",
                _options.MqttBrokerAddress, _options.MqttBrokerPort, i + 1, _settings.MqttMaxRetries);

            try
            {
                await _mqttClient.ConnectAsync(_clientOptions, cancellationToken);
                return;
            }
            catch (Exception e)
            {
                if (i < _settings.MqttMaxRetries - 1)
                {
                    _logger.LogWarning("Failed to connect to MQTT broker ({Reason)}. Retrying in {Delay}ms...",
                        e.Message, _settings.MqttDelayBetweenRetriesMs);

                    await Task.Delay(_settings.MqttDelayBetweenRetriesMs, cancellationToken);
                }
                else
                    _logger.LogError(e, "Failed to connect to MQTT broker: {Reason}", e.Message);
            }
        }
    }

    private async Task HandleConnectedAsync(MqttClientConnectedEventArgs e)
    {
        _logger.LogDebug("MQTT client connected to {Server}:{Port}",
            _options.MqttBrokerAddress, _options.MqttBrokerPort);

        MqttTopicFilter? topicFilter = null;
        if (!string.IsNullOrWhiteSpace(_options.MqttTopicFilter))
        {
            topicFilter = new MqttTopicFilterBuilder()
                .WithTopic(_options.MqttTopicFilter)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithNoLocal(true)
                .Build();
        }

        await _mqttClient.SubscribeAsync(topicFilter);
        _logger.LogDebug("Subscribed to MQTT topic: {TopicFilter}", _options.MqttTopicFilter ?? "(all)");
    }

    private async Task HandleClientDissconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        Debug.Assert(_mqttClient is not null);
        Debug.Assert(_clientOptions is not null);

        _logger.LogWarning("MQTT client disconnected from broker. Reason: {Reason}", e.Reason);
        if (CanReconnect())
        {
            await Task.Delay(_settings.MqttDelayBetweenRetriesMs, CancellationToken.None);
            await ConnectAsync(CancellationToken.None);
        }

        bool CanReconnect()
        {
            return e.Reason switch
            {
                MqttClientDisconnectReason.ConnectionRateExceeded => true,
                MqttClientDisconnectReason.ImplementationSpecificError => true,
                MqttClientDisconnectReason.KeepAliveTimeout => true,
                MqttClientDisconnectReason.MaximumConnectTime => true,
                MqttClientDisconnectReason.MessageRateTooHigh => true,
                MqttClientDisconnectReason.ReceiveMaximumExceeded => true,
                MqttClientDisconnectReason.ServerBusy => true,
                MqttClientDisconnectReason.UnspecifiedError => true,
                _ => false
            };
        }
    }

    private Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var topic = arg.ApplicationMessage.Topic;
        _logger.LogTrace("Received MQTT message on topic '{Topic}'", topic);

        var parsedTopic = MqttTopicTranslator.FromTinkwellToPlain(topic);
        if (parsedTopic is null)
        { 
            _logger.LogError("Unexpected MQTT message with topic {Topic}", topic);
            return Task.CompletedTask;
        }

        if (_repository.TryGetByExternalReferenceId(parsedTopic.Value.HostExternalReferenceId, out var host))
        {
            var payload = arg.ApplicationMessage.ConvertPayloadToString();
            _queue.EnqueueIncomingMessage(new MqttMessage(host.Id, parsedTopic.Value.Topic, payload));
        }
        else
        {
            _logger.LogError("MQTT message {Topic} targets an unknown host", topic);
        }

        return Task.CompletedTask;
    }
}
