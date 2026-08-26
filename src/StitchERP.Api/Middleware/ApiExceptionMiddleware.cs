using System.Net;
using System.Text.Json;

namespace StitchERP.Api.Middleware;

public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ApiExceptionMiddleware> logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var traceId = context.TraceIdentifier;
            logger.LogError(exception, "Unhandled API exception. TraceId: {TraceId}", traceId);
            context.Response.StatusCode = exception switch
            {
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                InvalidOperationException => StatusCodes.Status409Conflict,
                _ => (int)HttpStatusCode.InternalServerError
            };
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = context.Response.StatusCode switch
                {
                    StatusCodes.Status400BadRequest => "VALIDATION_ERROR",
                    StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
                    StatusCodes.Status404NotFound => "NOT_FOUND",
                    StatusCodes.Status409Conflict => "BUSINESS_RULE_VIOLATION",
                    _ => "INTERNAL_SERVER_ERROR"
                },
                message = context.Response.StatusCode == (int)HttpStatusCode.InternalServerError ? "An unexpected error occurred." : exception.Message,
                traceId
            }));
        }
    }
}
