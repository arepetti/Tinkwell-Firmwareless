namespace Tinkwell.Firmwareless;

public class TinkwellException : Exception
{
    public TinkwellException(string message) : base(message) { }
    public TinkwellException(string message, Exception innerException) : base(message, innerException) { }
}