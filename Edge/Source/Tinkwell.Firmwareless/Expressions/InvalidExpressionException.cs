namespace Tinkwell.Firmwareless.Expressions;

public sealed class InvalidExpressionException : TinkwellException
{
    public InvalidExpressionException(string message) : base(message) { }
    public InvalidExpressionException(string message, Exception innerException) : base(message, innerException) { }
}
