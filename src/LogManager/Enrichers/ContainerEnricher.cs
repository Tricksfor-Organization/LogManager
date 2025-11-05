using Serilog.Core;
using Serilog.Events;

namespace LogManager.Enrichers;

/// <summary>
/// Enricher to add Docker/Kubernetes container information
/// </summary>
public class ContainerEnricher : ILogEventEnricher
{
    private readonly Lazy<string?> _containerId;
    private readonly Lazy<string?> _containerName;
    private readonly Lazy<string?> _podName;
    private readonly Lazy<string?> _namespace;

    public ContainerEnricher()
    {
        _containerId = new Lazy<string?>(() => GetEnvironmentVariable("HOSTNAME"));
        _containerName = new Lazy<string?>(() => GetEnvironmentVariable("CONTAINER_NAME"));
        _podName = new Lazy<string?>(() => GetEnvironmentVariable("POD_NAME"));
        _namespace = new Lazy<string?>(() => GetEnvironmentVariable("POD_NAMESPACE"));
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!string.IsNullOrEmpty(_containerId.Value))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ContainerId", _containerId.Value));
        }

        if (!string.IsNullOrEmpty(_containerName.Value))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ContainerName", _containerName.Value));
        }

        if (!string.IsNullOrEmpty(_podName.Value))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("PodName", _podName.Value));
        }

        if (!string.IsNullOrEmpty(_namespace.Value))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Namespace", _namespace.Value));
        }
    }

    private static string? GetEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }
}
