namespace LogManager.Configuration;

/// <summary>
/// Configuration for Grafana Loki sink
/// </summary>
public class LokiOptions
{
    /// <summary>
    /// Enable Loki logging
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Loki server URL
    /// </summary>
    public string Url { get; set; } = "http://loki:3100";

    /// <summary>
    /// Static labels to attach to all log entries
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Username for basic authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for basic authentication (consider using HashiCorp Vault)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenant Loki setups
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Number of events to buffer before sending
    /// </summary>
    public int BatchPostingLimit { get; set; } = 1000;

    /// <summary>
    /// Time period to wait between checking for event batches
    /// </summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Queue size limit for buffered logs
    /// </summary>
    public int? QueueLimit { get; set; } = 100000;

    /// <summary>
    /// Output template for Loki messages
    /// </summary>
    public string OutputTemplate { get; set; } = 
        "[{Level:u3}] {Message:lj}{NewLine}{Exception}";
}
