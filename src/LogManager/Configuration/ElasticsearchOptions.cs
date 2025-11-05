namespace LogManager.Configuration;

/// <summary>
/// Configuration for Elasticsearch sink (ELK Stack)
/// </summary>
public class ElasticsearchOptions
{
    /// <summary>
    /// Enable Elasticsearch logging
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Elasticsearch node URIs (supports multiple nodes for cluster)
    /// </summary>
    public List<string> NodeUris { get; set; } = new() { "http://elasticsearch:9200" };

    /// <summary>
    /// Index format pattern (e.g., "logs-myapp-{0:yyyy.MM.dd}")
    /// </summary>
    public string IndexFormat { get; set; } = "logs-{0:yyyy.MM.dd}";

    /// <summary>
    /// Username for Elasticsearch authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for Elasticsearch authentication (consider using HashiCorp Vault)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// API key for authentication (alternative to username/password)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Number of events to buffer before sending to Elasticsearch
    /// </summary>
    public int BatchPostingLimit { get; set; } = 50;

    /// <summary>
    /// Time period to wait between checking for event batches
    /// </summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Auto-register index template
    /// </summary>
    public bool AutoRegisterTemplate { get; set; } = true;

    /// <summary>
    /// Number of shards for the index
    /// </summary>
    public int? NumberOfShards { get; set; } = 1;

    /// <summary>
    /// Number of replicas for the index
    /// </summary>
    public int? NumberOfReplicas { get; set; } = 1;

    /// <summary>
    /// Emit event failure for debugging
    /// </summary>
    public bool EmitEventFailure { get; set; } = true;

    /// <summary>
    /// Connection timeout
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
