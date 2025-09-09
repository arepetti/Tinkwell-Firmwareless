namespace Tinkwell.Firmwareless.WamrAotHost;

enum RequiredService
{
    Host,
    Coordinator
}

// We could use a library for this but between AOT trimming (Spectre), compatibility with
// Microsoft.Extensions.Hosting (current beta of System.CommandLine) and bloating...it's faster/easier
// to "parse" it: the use case for this app is EXTREMELY simple and what we need is basically a
// list of key-value pairs passed from the command line.
//
//
// Synopsis (coordinator):
//
// WamrAotHost coordinator --path=<PATH> [--mqtt-broker-address=<ADDRESS>] [--mqtt-broker-port=<PORT>]
//     [--mqtt-client-id=<ID>] [--mqtt-topic-filter=<FILTER>] [--transient]
//
// Starts and manages the wasm host processes for the specified firmlets and handle the communication between wasm hosts
// and the host system (outside the Docker container).
//
// Where:
//
// --path<PATH>
//     Required. Base directory where firmwares to load are saved. It must contains the firmware list firmwares.txt.
// --mqtt-broker-address=<ADDRESS>
//     Address of the MQTT broker. Can be omitted if specified using the env var TW_MQTT_BROKER_ADDRESS.
// --mqtt-broker-port<PORT>
//     Port to contact the MQTT broker. Can be omitted if specified using the env var TW_MQTT_BROKER_PORT.
// --mqtt-client-id=<ID>
//     Optional. ID of the MQTT client. Can be specified using the env var TW_MQTT_CLIENT.
//     If absent it defaults to "wasm-hosts-container".
// --mqtt-topic-filter=<FILTER>
//     Optional. Filter for the MQTT topic that determines the messages received by the firmlets. If omitted it
//     defaults to "tinkwell/#". You do not usually need to change this parameter.
// --transient
//     Optional. Exit immediately after the initialization phase. Used only for testing.
//
//
// Synopsis (host):
//
// WamrAotHost host --path=<PATH> --id=<ID> --pipe-name=<NAME> [--transient]
//
// "WamrAotHost host" is called by the coordinator to instantiate each host and should never be called directly
//
// Where:
//
// --path<PATH>
//     Required. Directory where the firmlet(s) to load are stored. Note that ALL the WASM modules (both *.wasm and *.aot)
//     are loaded in the host: a firmlet could be composed of multiple indipendent modules!
// --id=<ID>
//     Required. Unique ID associated with this host. This ID might change when the system (or the process) is restarted.
// --pipe-name=<NAME>
//     Required. Name of the pipe used to communicate (using JSON RPC) with the coordinator.
// --transient
//     Optional. Exit immediately after the initialization phase. Used only for testing.
//
sealed class CommandLineParser
{
    public CommandLineParser(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: A command ('coordinator' or 'host') is required.");
            Environment.Exit(1);
        }

        RequiredService = args[0] switch
        {
            "host" => RequiredService.Host,
            "coordinator" => RequiredService.Coordinator,
            _ => throw new HostException($"Unkown command '{args[0]}'.")
        };

        _options = ParseOptions(args);
    }

    public RequiredService RequiredService { get; init; }

    public string Name
    {
        get
        {
            if (RequiredService == RequiredService.Host)
                return $"{RequiredService} {GetOption("id")}";

            return $"{RequiredService} {GetOption("path")}";
        }
    }

    public CoordinatorServiceOptions GetCoordinatorServiceOptions()
    {
        return new(
            Path: GetOption("path"),
            MqttBrokerAddress: GetOption("mqtt-broker-address", true, Environment.GetEnvironmentVariable("TW_MQTT_BROKER_ADDRESS") ?? ""),
            MqttBrokerPort: ushort.Parse(GetOption("mqtt-broker-port", true, Environment.GetEnvironmentVariable("TW_MQTT_BROKER_PORT") ?? "")),
            MqttClientId: GetOption("mqtt-client-id", false, Environment.GetEnvironmentVariable("TW_MQTT_CLIENT_ID") ?? "wasm-hosts-container"),
            MqttTopicFilter: GetOption("mqtt-topic-filter", false, "tinkwell/#"),
            Transient: IsPresent("transient")
        );
    }

    public HostServiceOptions GetHostServiceOptions()
    {
        return new(
            Path: GetOption("path"),
            Id: GetOption("id"),
            PipeName: GetOption("pipe-name"),
            Transient: IsPresent("transient")
        );
    }

    private readonly IReadOnlyDictionary<string, string?> _options;

    private string GetOption(string optionName, bool required = true, string defaultIfMissing = "")
    {
        var key = $"--{optionName}";
        if (_options.TryGetValue(key, out var value) && value is not null)
            return value;

        if (required && string.IsNullOrWhiteSpace(defaultIfMissing))
        {
            Console.Error.WriteLine($"Error: Required option '{key}' is missing or has no value.");
            Environment.Exit(1);
        }

        return defaultIfMissing;
    }

    private bool IsPresent(string optionName)
        => _options.ContainsKey($"--{optionName}");

    private static Dictionary<string, string?> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--"))
                continue;

            var parts = arg.Split('=', 2);
            var key = parts[0];

            options[key] = parts.Length == 2 ? parts[1] : null;
        }
        return options;
    }
}
