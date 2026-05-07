namespace ProjectCallisto.API.Middleware;

/// <summary>
/// Middleware that generates and tracks correlation IDs for request tracing.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get correlation ID from incoming request header
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();

        // If not provided, generate a new one
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        // Store in HttpContext for access by controllers and services
        context.Items["CorrelationId"] = correlationId;

        // Add to response headers for client-side tracking
        context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);

        // Add to TraceIdentifier for use in ASP.NET Core logging
        context.TraceIdentifier = correlationId;

        await _next(context);
    }
}

/// <summary>
/// Extension method to add correlation ID middleware to the pipeline.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
