using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace Tinkwell.Firmwareless.Config.Parser;

public static class Keywords
{
    public const string Import = "import";
    public const string True = "true";
    public const string False = "false";
}

public enum ConfigToken
{
    Comment,
    Identifier,
    Import,
    QuotedString,
    TemplateString,
    ExpressionString,
    BracketedString,
    Number,
    True,
    False,
    LBrace,
    RBrace,
    Colon
}

public static class ConfigTokenizer
{
    public static Tokenizer<ConfigToken> Create()
    {
        return new TokenizerBuilder<ConfigToken>()
            .Ignore(Span.WhiteSpace)
            .Match(Comment.ShellStyle, ConfigToken.Comment)
            .Match(Span.EqualTo(Keywords.Import), ConfigToken.Import, requireDelimiters: true)
            .Match(Span.EqualTo(Keywords.True), ConfigToken.True, requireDelimiters: true)
            .Match(Span.EqualTo(Keywords.False), ConfigToken.False, requireDelimiters: true)
            .Match(Identifier.CStyle, ConfigToken.Identifier, requireDelimiters: true)
            .Match(Span.Regex(@"\$""(?:\\.|[^""])*"""), ConfigToken.TemplateString)
            .Match(Span.Regex(@"@""(?:\\.|[^""])*"""), ConfigToken.ExpressionString)
            .Match(QuotedString.CStyle, ConfigToken.QuotedString)
            .Match(Span.Regex(@"\[([^\]]*)\]"), ConfigToken.BracketedString)
            .Match(Numerics.Decimal, ConfigToken.Number)
            .Match(Span.EqualTo("{"), ConfigToken.LBrace)
            .Match(Span.EqualTo("}"), ConfigToken.RBrace)
            .Match(Span.EqualTo(":"), ConfigToken.Colon)
            .Build();
    }
}
