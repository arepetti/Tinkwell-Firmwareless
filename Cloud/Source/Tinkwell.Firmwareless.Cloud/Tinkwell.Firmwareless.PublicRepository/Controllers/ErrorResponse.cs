namespace Tinkwell.Firmwareless.PublicRepository.Controllers;

sealed record ErrorResponse(string Message, string? ParameterName = default);