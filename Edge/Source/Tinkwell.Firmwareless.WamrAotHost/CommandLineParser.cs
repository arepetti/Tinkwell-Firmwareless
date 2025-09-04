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
            MqttBrokerAddress: GetOption("mqtt-broker-address"),
            MqttBrokerPort: ushort.Parse(GetOption("mqtt-broker-port")),
            MqttClientId: GetOption("mqtt-client-id"),
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

        if (required)
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
