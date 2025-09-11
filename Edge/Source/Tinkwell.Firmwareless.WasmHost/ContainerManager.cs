using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;

namespace Tinkwell.Firmwareless.WasmHost;

sealed class ContainerManager(ILogger<HostedService> logger, IDockerClient docker, IOptions<Settings> options) : IContainerManager
{
    public async Task StartAsync(string baseDirectory, CancellationToken cancellationToken)
    {
        _baseDirectory = baseDirectory;

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        _logger.LogDebug("Starting Docker container...");

        await SetupNetworkInterfaceAsync(NetworkName);
        await InstallContainerImageAsync(cancellationToken);
        await CreateContainerAsync(cancellationToken);

        _logger.LogDebug("Starting container {ID}...", _containerId);
        await _docker.Containers.StartContainerAsync(_containerId, new ContainerStartParameters(), cancellationToken);

        StartAsyncLogCollection(cancellationToken);

        stopwatch.Stop();
        _logger.LogInformation("Container started in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_containerId is not null)
        {
            _logger.LogDebug("Shutting down container {ContainerName}", _containerName);

            var stopParams = new ContainerStopParameters { WaitBeforeKillSeconds = (uint)_settings.ContainerShutdownTimeoutSeconds };
            await _docker.Containers.StopContainerAsync(_containerId, stopParams, cancellationToken);

            var removeParams = new ContainerRemoveParameters { Force = true };
            await _docker.Containers.RemoveContainerAsync(_containerId, removeParams, cancellationToken);
        }
        _logger.LogInformation("Container stopped");
    }

    const string ContainerFirmwarePath = "/mnt/firmlets";
    const string NetworkName = "mqtt_only_net";

    private readonly ILogger<HostedService> _logger = logger;
    private readonly IDockerClient _docker = docker;
    private readonly Settings _settings = options.Value;
    private string? _baseDirectory;
    private string? _containerName;
    private string? _containerId;

    private async Task CreateContainerAsync(CancellationToken cancellationToken)
    {
        var createParams = new CreateContainerParameters
        {
            Image = $"{_settings.ImageName}:{_settings.ImageTag}",
            Name = _containerName = $"{_settings.ImageName}-{_settings.ImageTag}",
            Cmd =
            [
                "coordinator",
                $"--path={ContainerFirmwarePath}",
                $"--mqtt-broker-address={_settings.MqttBrokerAddress}",
                $"--mqtt-broker-port={_settings.MqttBrokerPort}"
            ],
            HostConfig = new HostConfig
            {
                ReadonlyRootfs = true,
                Binds =
                [
                    $"{_baseDirectory}:{ContainerFirmwarePath}:ro"
                ],
                Tmpfs = new Dictionary<string, string>
                {
                    { "/tmp", "" },
                    { "/var/tmp", "" },
                    { "/var/cache", "" }
                },
                NetworkMode = NetworkName,
                ExtraHosts = ["host.docker.internal:host-gateway"],
                CapDrop = ["ALL"],
                CapAdd = ["CAP_IPC_LOCK", "CAP_KILL", "CAP_NET_RAW", "CAP_SYS_NICE"], // https://www.man7.org/linux/man-pages/man7/capabilities.7.html
                CPUQuota = _settings.ContainerCpuQuota,
                Memory = _settings.ContainerMaximumMemoryUsage,
            }
        };

        try
        {
            await Create();
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Container {ContainerName} already exists, removing it...", _containerName);
            await _docker.Containers.RemoveContainerAsync(_containerName, new ContainerRemoveParameters { Force = true }, cancellationToken);

            await Create();
        }

        await _docker.Networks.ConnectNetworkAsync("bridge", new() { Container = _containerId }, cancellationToken);

        async Task Create()
        {
            _logger.LogDebug("Creating container {ID} from image {Image}...", createParams.Name, createParams.Image);
            var container = await _docker.Containers.CreateContainerAsync(createParams, cancellationToken);
            _containerId = container.ID;
        }
    }

    private async Task InstallContainerImageAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Searching for the docker image...");
        var listParams = new ImagesListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["reference"] = new Dictionary<string, bool>
                {
                    [$"{_settings.ImageName}:{_settings.ImageTag}"] = true
                }
            }
        };

        var exists = (await _docker.Images.ListImagesAsync(listParams, cancellationToken)).Any();
        if (exists == false)
        {
            _logger.LogInformation("Loading {ImagePath}...", _settings.ImageTarFileName);
            using var tarStream = File.OpenRead(_settings.ImageTarFileName);
            var loadParams = new ImageLoadParameters { Quiet = true };
            await _docker.Images.LoadImageAsync(loadParams, tarStream, null, cancellationToken);
        }
    }

    private async Task SetupNetworkInterfaceAsync(string networkName)
    {
        _logger.LogDebug("Searching for network {NetworkName}", networkName);
        var existingNetworks = await _docker.Networks.ListNetworksAsync(
            new NetworksListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool> { [networkName] = true }
                }
            }
        );

        if (existingNetworks.Any() == false)
        {
            _logger.LogInformation("Creating network {NetworkName}", networkName);
            await _docker.Networks.CreateNetworkAsync(new NetworksCreateParameters
            {
                Name = NetworkName,
                Internal = true
            });
        }
    }

    private void StartAsyncLogCollection(CancellationToken stoppingToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var parameters = new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = true,
                    Tail = "0"
                };

                using var muxStream = await _docker.Containers.GetContainerLogsAsync(_containerId, tty: false, parameters, stoppingToken);
                var buffer = new byte[4096];
                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = await muxStream.ReadOutputAsync(buffer, 0, buffer.Length, stoppingToken);

                    if (result.EOF)
                        break;

                    string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.Log(ConsoleLogFormatter.ParseFirmwareLogLevel(text), "{Text}", text);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error collecting container logs");
            }
        }, stoppingToken);
    }
}