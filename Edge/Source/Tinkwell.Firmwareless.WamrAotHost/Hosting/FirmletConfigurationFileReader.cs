using System.Text.RegularExpressions;
using Tinkwell.Firmwareless.Config;
using Tinkwell.Firmwareless.Config.Parser;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed partial class FirmletConfigurationFileReader : ConfigFileReaderBase<FirmletConfiguration>
{
    protected override Task<FirmletConfiguration> PerformSemanticAnalysisAsync(IEnumerable<Statement> statements, ConfigFileReaderOptions options, CancellationToken cancellationToken)
    {
        var configuration = new FirmletConfiguration();

        foreach (var statement in statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (statement is not BlockStatement block)
                throw new ConfigurationException("Invalid configuration: only blocks are supported at the top level.");

            ProcessBlock(configuration, "", block, options, cancellationToken);
        }

        return Task.FromResult(configuration);
    }

    private const string KeywordConfig = "config";
    private static readonly Regex ValidateNameRegex = CreateValidateNameRegex();

    private static void ProcessBlock(FirmletConfiguration configuration, string basePath, BlockStatement block, ConfigFileReaderOptions options, CancellationToken cancellationToken)
    {
        if (block.Keyword.Value != KeywordConfig)
            throw new ConfigurationException("Invalid configuration: only configuration blocks are supported. Invalid block: " + block.Keyword);

        var name = block.Name?.Value;
        if (string.IsNullOrWhiteSpace(name))
            throw new ConfigurationException("Configuration block is missing a name.");

        ValidateName(name);

        foreach (var statement in block.Body)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (statement)
            {
                case KeyValueStatement kv:
                    var key = ValidateName(ResolveString(kv.Key, options));
                    var value = ResolveValue(kv.Value, options);
                    configuration.AddEntry(string.Join('/', [basePath, name, key]), value);
                    break;
                case BlockStatement bs:
                    ProcessBlock(configuration, string.Join('/', [basePath, name]), bs, options, cancellationToken);
                    break;
                default:
                    throw new ConfigurationException("Invalid configuration: unsupported statement type " + statement.GetType().Name);
            }
        }
    }

    private static string ValidateName(string name)
    {
        if (ValidateNameRegex.IsMatch(name))
            return name;

        throw new ArgumentException($"Name '{name}' is not a valid identifier.");
    }

    [GeneratedRegex(@"^(?!\s)(?!.*\s$)(?!.* {2})(?!\s+$)[^/\\]+$", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex CreateValidateNameRegex();
}
