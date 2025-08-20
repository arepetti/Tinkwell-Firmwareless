namespace Tinkwell.Firmwareless.PublicRepository.Services.Queries;

enum FilterOp { Eq, Contains }

sealed record FilterTerm(string Field, FilterOp Op, string Value);

static class FilterParser
{
    // e.g., "name~acme,role==Admin"
    public static IReadOnlyList<FilterTerm> Parse(string filter)
    {
        var list = new List<FilterTerm>();
        foreach (var raw in filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.Contains("==", StringComparison.Ordinal))
            {
                var parts = raw.Split("==", 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                    list.Add(new FilterTerm(parts[0], FilterOp.Eq, parts[1]));
            }
            else if (raw.Contains('~'))
            {
                var parts = raw.Split('~', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                    list.Add(new FilterTerm(parts[0], FilterOp.Contains, parts[1]));
            }
            else
                throw new ArgumentException($"Invalid filter term: {raw}", nameof(filter));
        }
        return list;
    }
}
