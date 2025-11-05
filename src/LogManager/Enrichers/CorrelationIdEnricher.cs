using Serilog.Core;
using Serilog.Events;

namespace LogManager.Enrichers;

/// <summary>
/// Enricher to add correlation ID to log events
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    private const string CorrelationIdPropertyName = "CorrelationId";
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public CorrelationIdEnricher(ICorrelationIdAccessor correlationIdAccessor)
    {
        _correlationIdAccessor = correlationIdAccessor ?? throw new ArgumentNullException(nameof(correlationIdAccessor));
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = _correlationIdAccessor.GetCorrelationId();
        
        if (!string.IsNullOrEmpty(correlationId))
        {
            var property = propertyFactory.CreateProperty(CorrelationIdPropertyName, correlationId);
            logEvent.AddPropertyIfAbsent(property);
        }
    }
}

/// <summary>
/// Interface for accessing correlation ID from various sources (HTTP context, gRPC context, etc.)
/// </summary>
public interface ICorrelationIdAccessor
{
    string? GetCorrelationId();
}

/// <summary>
/// Default implementation that uses AsyncLocal for correlation ID storage
/// </summary>
public class DefaultCorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    public string? GetCorrelationId()
    {
        return _correlationId.Value;
    }

    public static void SetCorrelationId(string? correlationId)
    {
        _correlationId.Value = correlationId;
    }
}
