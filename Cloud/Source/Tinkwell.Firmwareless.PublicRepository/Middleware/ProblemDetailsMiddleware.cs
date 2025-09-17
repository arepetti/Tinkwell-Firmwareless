using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using Tinkwell.Firmwareless.Exceptions;

namespace Tinkwell.Firmwareless.PublicRepository.Middleware;

public class ProblemDetailsMiddleware
{
    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception for request {Path}", context.Request.Path);

            var statusCode = exception switch
            {
                ForbiddenAccessException => (int)HttpStatusCode.Forbidden,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                NotFoundException => (int)HttpStatusCode.NotFound,
                FormatException => (int)HttpStatusCode.BadRequest,
                InvalidOperationException => (int)HttpStatusCode.BadRequest,
                TooBigException => (int)HttpStatusCode.RequestEntityTooLarge,
                ConflictException => (int)HttpStatusCode.Conflict,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                HttpRequestException x => x.StatusCode == HttpStatusCode.BadRequest ? (int)HttpStatusCode.BadRequest : (int)HttpStatusCode.ServiceUnavailable,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var title = exception switch
            {
                ForbiddenAccessException => HttpStatusCode.Forbidden.ToString(),
                UnauthorizedAccessException => HttpStatusCode.Unauthorized.ToString(),
                NotFoundException => HttpStatusCode.NotFound.ToString(),
                FormatException => HttpStatusCode.BadRequest.ToString(),
                InvalidOperationException => HttpStatusCode.BadRequest.ToString(),
                TooBigException => HttpStatusCode.RequestEntityTooLarge.ToString(),
                ConflictException => HttpStatusCode.Conflict.ToString(),
                ArgumentException => HttpStatusCode.BadRequest.ToString(),
                HttpRequestException x => x.StatusCode == HttpStatusCode.BadRequest ? x.StatusCode.ToString() : HttpStatusCode.ServiceUnavailable.ToString(),
                _ => HttpStatusCode.InternalServerError.ToString()
            };

            var detail = exception switch
            {
                ForbiddenAccessException => exception.Message,
                UnauthorizedAccessException => exception.Message,
                NotFoundException => exception.Message,
                FormatException => exception.Message,
                InvalidOperationException => exception.Message,
                TooBigException => exception.Message,
                ConflictException => exception.Message,
                ArgumentException => exception.Message,
                HttpRequestException x => "",
                _ => ""
            };

            var problem = new ProblemDetails
            {
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/" + statusCode,
                Title = title,
                Status = statusCode,
                Detail = detail,
                Instance = context.Request.Path
            };

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = statusCode;

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, options));
        }
    }

    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;
}
