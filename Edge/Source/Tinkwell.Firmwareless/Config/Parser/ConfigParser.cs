using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace Tinkwell.Firmwareless.Config.Parser;

public static class ConfigParser
{
    static TokenListParser<ConfigToken, StringValue> QuotedString { get; } =
        Token.EqualTo(ConfigToken.QuotedString)
            .Apply(Superpower.Parsers.QuotedString.CStyle)
            .Select(s => new StringValue(s, StringType.Literal));

    static TokenListParser<ConfigToken, StringValue> TemplateString { get; } =
        Token.EqualTo(ConfigToken.TemplateString)
            .Select(t => new StringValue(t.ToStringValue().Substring(2, t.Span.Length - 3), StringType.Template));

    static TokenListParser<ConfigToken, StringValue> ExpressionString { get; } =
        Token.EqualTo(ConfigToken.ExpressionString)
            .Select(t => new StringValue(t.ToStringValue().Substring(2, t.Span.Length - 3), StringType.Expression));

    static TokenListParser<ConfigToken, StringValue> AnyQuotedString { get; } =
        QuotedString.Or(TemplateString).Or(ExpressionString);

    static TokenListParser<ConfigToken, StringValue> Identifier { get; } =
        Token.EqualTo(ConfigToken.Identifier)
            .Select(t => new StringValue(t.ToStringValue(), StringType.Literal));

    static TokenListParser<ConfigToken, StringValue> Name { get; } =
        AnyQuotedString.Or(Identifier);

    static TokenListParser<ConfigToken, ValueNode> Number { get; } =
        Token.EqualTo(ConfigToken.Number)
            .Select(t => (ValueNode)new NumberValue(t.ToStringValue()));

    static TokenListParser<ConfigToken, ValueNode> Boolean { get; } =
        Token.EqualTo(ConfigToken.True).Value((ValueNode)new BooleanValue(true))
            .Or(Token.EqualTo(ConfigToken.False).Value((ValueNode)new BooleanValue(false)));

    static TokenListParser<ConfigToken, ValueNode> Value { get; } =
        AnyQuotedString.Cast<ConfigToken, StringValue, ValueNode>()
            .Or(Number)
            .Or(Boolean);

    static readonly TokenListParser<ConfigToken, Statement[]> StatementList =
        Parse.Ref(() => Statement!.Many());

    static TokenListParser<ConfigToken, Statement> Block { get; } =
        from keyword in Identifier
        from name in Name.Try().Or(input => TokenListParserResult.Value(default(StringValue), input, input)!)
        from lbrace in Token.EqualTo(ConfigToken.LBrace)
        from body in StatementList
        from rbrace in Token.EqualTo(ConfigToken.RBrace)
        select (Statement)new BlockStatement(keyword, name, body);

    static TokenListParser<ConfigToken, Statement> KeyValue { get; } =
        from key in Name
        from colon in Token.EqualTo(ConfigToken.Colon)
        from value in Value
        select (Statement)new KeyValueStatement(key, value);

    static TokenListParser<ConfigToken, Statement> Import { get; } =
        from import in Token.EqualTo(ConfigToken.Import)
        from path in QuotedString
        select (Statement)new ImportStatement(path);

    static TokenListParser<ConfigToken, Statement> Statement { get; } =
        Import.Try()
        .Or(Block.Try())
        .Or(KeyValue);

    static TokenListParser<ConfigToken, FileNode> File { get; } =
        (from statements in Statement.Many() select new FileNode(statements)).AtEnd();

    public static TokenListParserResult<ConfigToken, FileNode> TryParse(string source)
    {
        var tokenizer = ConfigTokenizer.Create();
        var tokens = tokenizer.Tokenize(source).Where(t => t.Kind != ConfigToken.Comment).ToArray();
        return File(new TokenList<ConfigToken>(tokens));
    }
}