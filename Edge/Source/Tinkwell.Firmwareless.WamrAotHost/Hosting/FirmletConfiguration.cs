namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class FirmletConfiguration
{
    public void AddEntry(string key, object? value) => _entries[key] = value;

    public object? GetEntry(string key, object? defaultValue = default!)
    {
        if (_entries.TryGetValue(key, out var value))
            return value;

        return defaultValue;
    }

    private readonly Dictionary<string, object?> _entries = [];
}
