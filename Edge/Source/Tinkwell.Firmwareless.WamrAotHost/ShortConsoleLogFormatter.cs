using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

sealed class ShortConsoleLogFormatter(IOptions<ConsoleFormatterOptions> options)
    : ConsoleFormatter(nameof(ShortConsoleLogFormatter))
{
    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter writer)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);

        if (string.IsNullOrEmpty(message))
            return;

        var fullCategory = logEntry.Category ?? "";
        var shortCategory = Shorten(fullCategory);
        var logLevel = Shorten(logEntry.LogLevel);

        if (logEntry.EventId.Id != 0)
            shortCategory += $"[{logEntry.EventId.Id}]";

        // Timestamp
        var now = _options.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
        writer.Write(now.ToString(_options.TimestampFormat ?? "HH:mm:ss.fff"));
        writer.Write(' ');

        // Log level and category
        writer.Write($"{logLevel} - {shortCategory}: {message}");

        // Exception
        if (logEntry.Exception is not null)
        {
            writer.Write(' ');
            writer.Write(logEntry.Exception);
        }

        writer.WriteLine();

        // Scopes
        if (_options.IncludeScopes && scopeProvider is not null)
        {
            scopeProvider.ForEachScope((scope, state) =>
            {
                state.WriteLine($"=> {scope}");
            }, writer);
        }
    }

    private readonly ConsoleFormatterOptions _options = options.Value;

    private static string Shorten(string category)
    {
        var lastDot = category.LastIndexOf('.');
        var name = lastDot >= 0 ? category[(lastDot + 1)..] : category;
        var tick = name.IndexOf('`');
        return tick >= 0 ? name[..tick] : name;
    }

    private static string Shorten(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "err ",
        LogLevel.Critical => "crit",
        _ => "????"
    };
}