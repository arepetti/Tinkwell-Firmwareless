namespace Tinkwell.Firmwareless.Controllers;

public sealed record ErrorResponse(string Message, string? ParameterName = default);
