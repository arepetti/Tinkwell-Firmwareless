using System.Globalization;
using Tinkwell.Firmwareless.Config.Parser;
using Tinkwell.Firmwareless.Expressions;

namespace Tinkwell.Firmwareless.Config;

public abstract class ConfigFileReaderBase<TConfig> : IConfigFileReader<TConfig> where TConfig : class, new()
{
    public async Task<TConfig> ReadAsync(string filePath, ConfigFileReaderOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var statements = await ConfigFileReaderBase<TConfig>.ReadAndExpandImportsAsync(Path.GetDirectoryName(filePath)!, filePath, options, cancellationToken);
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

    protected static string ResolveString(StringValue value, ConfigFileReaderOptions options)
    {
        return value.Type switch
        {
            StringType.Literal => value.Value,
            StringType.Template => TemplateRenderer.Render(value.Value, options.Parameters),
            StringType.Expression => new ExpressionEvaluator().EvaluateString(value.Value, options.Parameters),
            _ => throw new ConfigurationException($"Unknown string type: {value.Type}"),
        };
    }

    protected static double ResolveNumber(NumberValue value, ConfigFileReaderOptions options)
    {
        if (double.TryParse(value.Value, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new ConfigurationException($"Invalid number: {value.Value}");
    }

    protected static object? ResolveValue(ValueNode value, ConfigFileReaderOptions options)
    {
        if (value is null)
            return null;

        if (value is BooleanValue bv)
            return bv.Value;

        if (value is NumberValue nv)
            return ResolveNumber(nv, options);

        if (value is StringValue sv)
        {
            return sv.Type switch
            {
                StringType.Literal => sv.Value,
                StringType.Template => TemplateRenderer.Render(sv.Value, options.Parameters),
                StringType.Expression => new ExpressionEvaluator().Evaluate(sv.Value, options.Parameters),
                _ => throw new ConfigurationException($"Unknown string type: {sv.Type}"),
            };
        }

        throw new ConfigurationException("Invalid configuration: unsupported value type " + value.GetType().Name);
    }

    private static async Task<IEnumerable<Statement>> ReadAndExpandImportsAsync(string basePath, string filePath, ConfigFileReaderOptions options, CancellationToken cancellationToken)
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
            statements.AddRange(await ConfigFileReaderBase<TConfig>.ReadAndExpandImportsAsync(newBasePath, importPath, options, cancellationToken));
        }

        return statements;
    }
}
