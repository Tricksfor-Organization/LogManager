using Serilog.Core;
using Serilog.Events;

namespace LogManager.Enrichers;

/// <summary>
/// Enricher to add service name and version to log events
/// </summary>
public class ServiceContextEnricher : ILogEventEnricher
{
    private readonly string _serviceName;
    private readonly string _serviceVersion;
    private readonly string _serviceId;

    public ServiceContextEnricher(string serviceName, string serviceVersion, string? serviceId = null)
    {
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
        _serviceVersion = serviceVersion ?? throw new ArgumentNullException(nameof(serviceVersion));
        _serviceId = serviceId ?? Guid.NewGuid().ToString();
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ServiceName", _serviceName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ServiceVersion", _serviceVersion));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ServiceId", _serviceId));
    }
}
