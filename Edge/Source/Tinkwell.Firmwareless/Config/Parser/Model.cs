namespace Tinkwell.Firmwareless.Config.Parser;

public abstract record AstNode;

// Value Nodes
public abstract record ValueNode : AstNode;
public sealed record StringValue(string Value, StringType Type = StringType.Literal) : ValueNode;
public enum StringType { Literal, Template, Expression }
public sealed record NumberValue(string Value) : ValueNode;
public sealed record BooleanValue(bool Value) : ValueNode;

// Statement Nodes
public sealed record FileNode(Statement[] Statements) : AstNode;
public abstract record Statement : AstNode;
public sealed record ImportStatement(StringValue Path) : Statement;
public sealed record KeyValueStatement(StringValue Key, ValueNode Value) : Statement;
public sealed record BlockStatement(StringValue Keyword, StringValue? Name, Statement[] Body) : Statement;
