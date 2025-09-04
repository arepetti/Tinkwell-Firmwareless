using System.Text.RegularExpressions;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;

// Here there is a bit of magic. To address a specific firmware (and device) we expect all
// messages to start with "tinkwell/<FIRMWARE ID>/*". Where <FIRMWARE ID> is stored here as "external reference ID".
// We strip this prefix and send the message "naked" to the firmlet, the same way that a device won't receive
// that prefix. It's used internally by Tinkwell to dispatch these messages to the correct destination.
static partial class MqttTopicTranslator
{
    public static (string HostId, string Topic)? FromTinkwellToPlain(string topic)
    {

        var match = ParseTopicRegex().Match(topic);
        if (match.Success)
            return (match.Groups[1].Value, match.Groups[2].Value);

        return null;
    }

    public static string FromPlainToTinkwell(HostInfo host, string topic)
        => $"tinkwell/{host.ExternalReferenceId}/{topic}";

    [GeneratedRegex(@"^tinkwell/([A-Za-z0-9_-]+)/?(.*)$")]
    private static partial Regex ParseTopicRegex();
}