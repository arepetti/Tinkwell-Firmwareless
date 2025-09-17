using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Tinkwell.Firmwareless.WasmHost;

sealed class ConsoleLogFormatter(IOptions<ConsoleFormatterOptions> options)
    : ConsoleFormatter(nameof(ConsoleLogFormatter))
{
    public static readonly EventId FirmwareEntry = new(2025, "Firmware");

    public static LogLevel ParseFirmwareLogLevel(string logMessage)
    {
        var parts = logMessage.Split(' ', 3, StringSplitOptions.None);
        if (parts.Length < 3)
            return LogLevel.Trace; // Just stdout output not coming from logging

        return parts[1] switch
        {
            "trce" or "trace" => LogLevel.Trace,
            "dbug" or "debug" => LogLevel.Debug,
            "info" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "err" or "error" => LogLevel.Error,
            "crit" or "critical" => LogLevel.Critical,
            _ => LogLevel.Trace
        };
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter writer)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);

        if (string.IsNullOrEmpty(message))
            return;

        if (logEntry.EventId == FirmwareEntry)
        {
            WriteFirmwareEntry(writer, message);
            return;
        }

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
        var color = GetColor(logEntry.LogLevel);
        writer.Write($"{color}{logLevel}{Reset} - {shortCategory}: {message}");

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

    private const string Reset = "\x1b[0m";
    private const string Gray = "\x1b[37m";
    private const string Green = "\x1b[32m";
    private const string Yellow = "\x1b[33m";
    private const string Red = "\x1b[31m";

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

    private static string GetColorForLevel(string level) => level switch
    {
        "trce" or "trace" => Gray,
        "dbug" or "debug" => Gray,
        "info" => Green,
        "warn" or "warning" => Yellow,
        "err" or "error" or "crit" or "critical" => Red,
        _ => Reset
    };

    private static string GetColor(LogLevel level) => level switch
    {
        LogLevel.Trace => Gray,
        LogLevel.Debug => Gray,
        LogLevel.Information => Green,
        LogLevel.Warning => Yellow,
        LogLevel.Error or LogLevel.Critical => Red,
        _ => Reset
    };

    private static void WriteFirmwareEntry(TextWriter writer, string message)
    {
        var lines = message.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i=0; i < lines.Length; ++i)
        {
            var line = lines[i];
            if (i > 0)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    writer.WriteLine(line);
            }
            else
            {
                var parts = line.Split(' ', 3, StringSplitOptions.None);
                if (parts.Length < 3)
                {
                    writer.WriteLine(line);
                }
                else
                {
                    var timestamp = parts[0];
                    var level = parts[1];
                    var rest = parts[2];

                    var color = GetColorForLevel(level);
                    writer.Write($"{timestamp} {color}{level}{Reset} {rest}");
                }
            }
        }
    }
}