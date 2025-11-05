using Microsoft.AspNetCore.Http;
using LogManager.Enrichers;

namespace LogManager.Middleware;

/// <summary>
/// Middleware to capture and propagate correlation ID from HTTP headers
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    // Instance method is required by ASP.NET Core middleware pattern to access _next field
#pragma warning disable CA1822 // Mark members as static
    public async Task InvokeAsync(HttpContext context)
#pragma warning restore CA1822 // Mark members as static
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Set correlation ID in AsyncLocal storage
        DefaultCorrelationIdAccessor.SetCorrelationId(correlationId);

        // Add to response headers for client tracking
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // Try to get from request headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId) &&
            !string.IsNullOrEmpty(correlationId))
        {
            return correlationId.ToString();
        }

        // Try alternative headers
        if (context.Request.Headers.TryGetValue("X-Request-ID", out var requestId) &&
            !string.IsNullOrEmpty(requestId))
        {
            return requestId.ToString();
        }

        // Generate new correlation ID
        return Guid.NewGuid().ToString();
    }
}
