using System.Text.RegularExpressions;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

partial class CompilerOptionsBuilder
{
    public void UseValidation(string path)
    {
        var config = ValidationRulesParser.Load(path);

        ValidateComponents(config.Validate.Components);
        ValidateWithRules(config.Validate.Rules);
    }

    private static bool IsMatchForValueSet(string value, ValidationRulesParser.ValueSet rules)
    {
        if (rules.Any is not null && rules.Any.Contains(value))
            return true;

        if (rules.AnyRegex is not null && rules.AnyRegex.Any(x => Regex.IsMatch(value, x, RegexOptions.CultureInvariant)))
            return true;

        return false;
    }

    private static bool IsMatchForValueOrValueSet(string value, ValidationRulesParser.ValueOrValueSet? rules)
    {
        if (rules is null)
            return true;

        if (rules.IsString && value != rules.Value)
            return false;
        
        if (rules.IsSet && !IsMatchForValueSet(value, rules.Set))
            return false;

        return true;
    }

    private void ValidateComponents(Dictionary<string, ValidationRulesParser.ValueSet> rules)
    {
        foreach (var entry in rules)
        {
            var value = entry.Key switch
            {
                ArchitectureKey => _architecture,
                VendorKey => _vendor,
                OsKey => _os,
                AbiKey => _abi,
                _ => throw new InvalidOperationException($"Unknown validation component: {entry.Key}")
            };

            if (!IsMatchForValueSet(value, entry.Value))
                throw new ArgumentException($"Invalid value '{value}' for component '{entry.Key}'. ");
        }
    }

    private void ValidateWithRules(IEnumerable<ValidationRulesParser.ValidationRule> rules)
    {
        // We go through them all instead of stopping at the first failed match because
        // some rules are fairly relaxed with their requirements and it's easier if we do not need
        // to observe a strict ordering in this file (the rules are not ordered by priority).
        // It's important (and enforced) when generating the compiler options, not here.
        bool matched = false;
        foreach (var rule in rules)
        {
            if (IsValidationRuleProfileMatch(rule.When))
            {
                if (IsValidationRuleProfileMatch(rule.Require))
                    matched = true;
            }
        }

        if (!matched)
            throw new ArgumentException($"{_originalTarget} is not a valid or supported combination.");
    }

    private bool IsValidationRuleProfileMatch(ValidationRulesParser.MatchProfile? profile)
    {
        if (profile is null)
            return true;

        if (!IsMatchForValueOrValueSet(_architecture, profile.Architecture))
            return false;

        if (!IsMatchForValueOrValueSet(_vendor, profile.Vendor))
            return false;

        if (!IsMatchForValueOrValueSet(_os, profile.Os))
            return false;

        if (!IsMatchForValueOrValueSet(_abi, profile.Abi))
            return false;

        return true;
    }
}