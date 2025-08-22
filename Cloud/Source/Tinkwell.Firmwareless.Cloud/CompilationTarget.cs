using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Tinkwell.Firmwareless;

public sealed partial record CompilationTarget(
    string Architecture,
    string Vendor,
    string Os,
    string Abi,
    string[] Features
)
{
    public const char SectionSeparator = '~';

    public bool IsMeta
        => string.IsNullOrEmpty(Vendor);

    public override string ToString()
    {
        var result = new StringBuilder(Architecture ?? string.Empty);

        // If one is present then they all are
        if (!string.IsNullOrEmpty(Vendor))
            result.AppendFormat("-{0}-{1}-{2}", Vendor, Os, Abi);

        if (Features.Length > 0)
        {
            result.Append(SectionSeparator);
            result.AppendJoin(',', Features);
        }

        return result.ToString();
    }

    public static CompilationTarget Parse(string input)
    {
        if (!TryParseImpl(input, out var spec, out var error))
            throw new FormatException(error);

        return spec;
    }

    public static bool TryParse(string input, [NotNullWhen(true)] out CompilationTarget? spec)
        => TryParseImpl(input, out spec, out _);

    // architecture[-vendor-os-abi][~opt1,opt2,...]
    private static bool TryParseImpl(
        string input,
        [NotNullWhen(true)] out CompilationTarget? spec,
        [NotNullWhen(false)] out string? error
    )
    {
        spec = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Input is null, empty or composed of white spaces.";
            return false;
        }

        var m = Pattern.Match(input);
        if (!m.Success)
        {
            error = "Expected architecture[-vendor-os-abi][~opt1,opt2,...] with only letters, digits, or underscores in each token.";
            return false;
        }

        var arch = m.Groups["arch"].Value;
        var vendor = m.Groups["vendor"].Success ? m.Groups["vendor"].Value : string.Empty;
        var os = m.Groups["os"].Success ? m.Groups["os"].Value : string.Empty;
        var abi = m.Groups["abi"].Success ? m.Groups["abi"].Value : string.Empty;

        var options = m.Groups["options"].Success
            ? m.Groups["options"].Value.Split(',', StringSplitOptions.None)
            : [];

        spec = new CompilationTarget(
            Architecture: arch,
            Vendor: vendor,
            Os: os,
            Abi: abi,
            Features: options
        );

        return true;
    }

    private static readonly Regex Pattern = CompilePatternRegex();

    [GeneratedRegex(@"^(?<arch>[A-Za-z0-9_]+)(?:-(?<vendor>[A-Za-z0-9_]+)-(?<os>[A-Za-z0-9_]+)-(?<abi>[A-Za-z0-9_]+))?(?:~(?<options>[A-Za-z0-9_]+(?:,[A-Za-z0-9_]+)*))?$", RegexOptions.Compiled)]
    private static partial Regex CompilePatternRegex();
}
