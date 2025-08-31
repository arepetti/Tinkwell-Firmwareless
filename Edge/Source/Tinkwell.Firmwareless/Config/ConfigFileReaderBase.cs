using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Firmwareless.Config.Parser;
using Tinkwell.Firmwareless.Expressions;

namespace Tinkwell.Firmwareless.Config;

public abstract class ConfigFileReaderBase<TConfig> : IConfigFileReader<TConfig> where TConfig : class, new()
{
    public async Task<TConfig> ReadAsync(string filePath, ConfigFileReaderOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var statements = await ReadAndExpandImportsAsync(Path.GetDirectoryName(filePath)!, filePath, options, cancellationToken);
            return await PerformSemanticAnalysisAsync(statements, options, cancellationToken);
        }
        catch (IOException e)
        {
            throw new ConfigurationException($"Error reading configuration file {filePath}", e);
        }
        catch (UnauthorizedAccessException e)
        {
            throw new ConfigurationException($"Access denied to configuration file {filePath}", e);
        }
        catch (Exception e) when (e is not ConfigurationException)
        {
            throw new ConfigurationException($"Unexpected error reading configuration file {filePath}", e);
        }
    }

    protected abstract Task<TConfig> PerformSemanticAnalysisAsync(IEnumerable<Statement> statements, ConfigFileReaderOptions options, CancellationToken cancellationToken);

    private async Task<IEnumerable<Statement>> ReadAndExpandImportsAsync(string basePath, string filePath, ConfigFileReaderOptions options, CancellationToken cancellationToken)
    {
        var source = await File.ReadAllTextAsync(filePath, cancellationToken);
        var parseResult = ConfigParser.TryParse(source);

        if (!parseResult.HasValue)
            throw new ConfigurationException($"Error parsing {filePath}: {parseResult.FormatErrorMessageFragment()}");

        var statements = parseResult.Value.Statements
            .Where(x => x is not ImportStatement)
            .ToList();

        if (options.NoIncludes)
            return statements;

        Queue<string> imports = new(
             parseResult.Value.Statements
                .OfType<ImportStatement>()
                .Select(x => ResolveString(x.Path, options))
        );

        while (imports.TryDequeue(out var import))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var importPath = Path.Combine(basePath, import);
            var newBasePath = Path.GetDirectoryName(importPath)!;
            statements.AddRange(await ReadAndExpandImportsAsync(newBasePath, importPath, options, cancellationToken));
        }

        return statements;
    }

    private static string ResolveString(StringValue value, ConfigFileReaderOptions options)
    {
        switch (value.Type)
        {
            case StringType.Literal:
                return value.Value;
            case StringType.Template:
                return TemplateRenderer.Render(value.Value, options.Parameters);
            case StringType.Expression:
                return new ExpressionEvaluator().EvaluateString(value.Value, options.Parameters);
            default:
                throw new ConfigurationException($"Unknown string type: {value.Type}");
        }
    }
}
