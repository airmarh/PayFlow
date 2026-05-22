using System.Net;
using System.Text.Json;
using PayFlow.Application.Exceptions;

namespace PayFlow.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            NotFoundException          => (HttpStatusCode.NotFound, "Resource Not Found"),
            ConflictException          => (HttpStatusCode.Conflict, "Conflict"),
            ConcurrencyException       => (HttpStatusCode.Conflict, "Concurrency Conflict"),
            InsufficientFundsException => (HttpStatusCode.UnprocessableEntity, "Insufficient Funds"),
            ValidationException        => (HttpStatusCode.BadRequest, "Validation Error"),
            PayFlowException           => (HttpStatusCode.BadRequest, "Bad Request"),
            _                          => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "{Title}: {Message}", title, exception.Message);
        }

        var problemDetail = new
        {
            type   = $"https://httpstatuses.com/{(int)statusCode}",
            title,
            status = (int)statusCode,
            detail = statusCode == HttpStatusCode.InternalServerError
                ? "An internal error occurred. Please try again later."
                : exception.Message,
            traceId = context.TraceIdentifier
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = (int)statusCode;

        var json = JsonSerializer.Serialize(problemDetail, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
