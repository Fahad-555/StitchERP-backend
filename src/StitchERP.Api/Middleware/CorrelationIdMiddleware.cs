namespace StitchERP.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        const string header = "X-Correlation-Id";
        var correlationId = context.Request.Headers[header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId)) correlationId = Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.Headers[header] = correlationId;
        await next(context);
    }
}
