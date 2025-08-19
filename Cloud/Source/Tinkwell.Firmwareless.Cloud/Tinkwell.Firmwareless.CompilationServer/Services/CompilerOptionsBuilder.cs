using Tinkkwell.Firmwareless;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

sealed class CompilerOptionsBuilderOptions
{
    public string? StackUsageFile { get; set; }
    public bool EnableMultiThread { get; set; }
    public bool EnableTailCall { get; set; } = true;
    public bool EnableGarbageCollection { get; set; }
    public bool VerboseLog { get; set; }
}

sealed partial class CompilerOptionsBuilder
{
    public CompilerOptionsBuilder(CompilationTarget target)
    {
        _originalTarget = target;
        _architecture = target.Architecture;
        _vendor = target.Vendor;
        _os = target.Os;
        _abi = target.Abi;
        _features.AddRange(target.Features);
    }

    public void WithFiles(IEnumerable<(string Input, string Output)> compilationUnits)
    {
        ArgumentNullException.ThrowIfNull(compilationUnits, nameof(compilationUnits));
        _compilationUnits = compilationUnits;
    }

    public void UseCompilerConfiguration(string path)
    {
        var config = CompilationConfigParser.Load(path);

        bool found = false;
        string target = $"{_architecture}-{_vendor}-{_os}-{_abi}";
        foreach (var entry in config.Flags)
        {
            if (TargetPattern.IsMatch(entry.Key, target))
            {
                _basicParameters.AddRange(entry.Value.Select(x => x.Replace("{vendor}", _vendor)));
                found = true;
                break;
            }
        }

        if (!found)
            throw new ArgumentException($"Architecture {_originalTarget} is not a supported target.");

        string architecture = _architecture;
        if (_architecture.StartsWith("arm"))
            architecture = "arm";

        if (!config.Features.TryGetValue(architecture, out var featureSet))
            return;

        foreach (var feature in _features)
        {
            if (featureSet.TryGetValue(feature, out var flags))
                _basicParameters.AddRange(flags);
            else
                throw new ArgumentException($"Feature '{feature}' is not supported for architecture '{architecture}'.");
        }
    }

    public string[] Build(CompilerOptionsBuilderOptions? options = default)
    {
        options ??= new();

        List<string> compilationUnits = new();
        foreach (var unit in _compilationUnits)
            compilationUnits.AddRange([$"-o", $"\"{unit.Output}\"", $"\"{unit.Input}\""]);

        string[] flags = _flags
            .Select(x => string.IsNullOrWhiteSpace(x.Value) ? $"-{x.Key}" : $"-{x.Key}={x.Value}")
            .ToArray();

        _basicParameters.Add("--invoke-c-api-import");
        if (options.EnableGarbageCollection)
            _basicParameters.Add("--enable-gc");
        if (options.EnableMultiThread)
            _basicParameters.Add("--enable-multi-thread");
        if (options.EnableTailCall)
            _basicParameters.Add("--enable-tail-call");
        if (!string.IsNullOrWhiteSpace(options.StackUsageFile))
            _basicParameters.Add($"--stack-usage=\"{options.StackUsageFile}\"");
        if (options.VerboseLog)
            _basicParameters.Add("-v=5");

        return [.. _basicParameters, .. flags, .. compilationUnits];
    }

    private readonly CompilationTarget _originalTarget;
    private string _architecture;
    private string _vendor;
    private string _os;
    private string _abi;
    private readonly List<string> _features = new();
    private readonly Dictionary<string, string> _flags = new();
    private readonly List<string> _basicParameters = new();
    private IEnumerable<(string Input, string Output)> _compilationUnits = [];
}