using Microsoft.AspNetCore.Builder;

namespace LogManager.Middleware;

/// <summary>
/// Extension methods for adding LogManager middleware
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// Add correlation ID middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
