namespace Tinkwell.Firmwareless.PublicRepository.Repositories;

sealed class NotFoundException : ArgumentException
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string message, string paramName) : base(message, paramName) { }
    public NotFoundException(Guid resourceId, string paramName) : base($"Resource '{resourceId}' cannot be found.", paramName) { }
    public NotFoundException() : base("The requested resource cannot be found.") { }    
}
