using Docker.DotNet.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using static Tinkwell.Firmwareless.CompilationServer.Services.ValidationRulesParser;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

static class TargetPattern
{
    // Matches whole string with a minimal syntax:
    // - '|' for alternation between whole-pattern alternatives
    // - '*' wildcard that DOES NOT cross dashes (expands to [^-]*)
    public static bool IsMatch(string pattern, string text)
        => ToRegex(pattern).IsMatch(text);

    public static Regex ToRegex(string pattern)
    {
        return Cache.GetOrAdd(pattern, _ => BuildRegex(pattern));
    }

    private static readonly ConcurrentDictionary<string, Regex> Cache = new();

    private static Regex BuildRegex(string pattern)
    {
        var alts = pattern.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sb = new StringBuilder();
        sb.Append("^(?:");
        for (int i = 0; i < alts.Length; ++i)
        {
            if (i > 0)
                sb.Append('|');
            sb.Append(ConvertAlt(alts[i]));
        }
        sb.Append(")$");

        return new Regex(sb.ToString(), RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static string ConvertAlt(string alt)
    {
        var sb = new StringBuilder(alt.Length * 2);
        foreach (char c in alt)
        {
            if (c == '*')
            {
                // starStopsAtDash = true
                sb.Append("[^-]*");
            }
            else
            {
                // Escape regex specials (except '*' handled above and '|' split already)
                if ("\\.^$+?()[]{}".Contains(c))
                    sb.Append('\\');

                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
