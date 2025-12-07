using Microsoft.Extensions.Logging;

namespace LogManager.Configuration;

/// <summary>
/// Main configuration options for LogManager
/// </summary>
public class LogManagerOptions
{
    /// <summary>
    /// Application name to be included in logs
    /// </summary>
    public string ApplicationName { get; set; } = "UnknownApp";

    /// <summary>
    /// Environment name (Development, Staging, Production)
    /// </summary>
    public string Environment { get; set; } = "Development";

    /// <summary>
    /// Minimum log level (Verbose, Debug, Information, Warning, Error, Fatal)
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Minimum log level as enum (preferred for code-based configuration)
    /// Uses Microsoft.Extensions.Logging.LogLevel
    /// </summary>
    public LogLevel? MinimumLevelEnum { get; set; }

    /// <summary>
    /// Enable console logging
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// File logging configuration
    /// </summary>
    public FileLoggingOptions? FileLogging { get; set; }

    /// <summary>
    /// Elasticsearch configuration
    /// </summary>
    public ElasticsearchOptions? Elasticsearch { get; set; }

    /// <summary>
    /// Grafana Loki configuration
    /// </summary>
    public LokiOptions? Loki { get; set; }

    /// <summary>
    /// Enrichment options
    /// </summary>
    public EnrichmentOptions Enrichment { get; set; } = new();

    /// <summary>
    /// Override minimum level for specific namespaces
    /// </summary>
    public Dictionary<string, string> MinimumLevelOverrides { get; set; } = new()
    {
        { "Microsoft", "Warning" },
        { "Microsoft.Hosting.Lifetime", "Information" },
        { "System", "Warning" }
    };
}
