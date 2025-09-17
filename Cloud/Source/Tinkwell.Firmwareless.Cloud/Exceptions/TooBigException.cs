namespace Tinkwell.Firmwareless.Exceptions;

public sealed class TooBigException : ArgumentException
{
    public TooBigException(string message) : base(message) { }
}
