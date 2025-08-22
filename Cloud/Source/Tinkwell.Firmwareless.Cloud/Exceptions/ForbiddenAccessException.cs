namespace Tinkwell.Firmwareless.Exceptions;

public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }
    public ForbiddenAccessException() : base("You are not authorized to access this resource.") { }
}
