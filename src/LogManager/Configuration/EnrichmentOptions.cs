namespace LogManager.Configuration;

/// <summary>
/// Configuration for log enrichment
/// </summary>
public class EnrichmentOptions
{
    /// <summary>
    /// Enrich with machine name
    /// </summary>
    public bool WithMachineName { get; set; } = true;

    /// <summary>
    /// Enrich with environment name
    /// </summary>
    public bool WithEnvironmentName { get; set; } = true;

    /// <summary>
    /// Enrich with thread ID
    /// </summary>
    public bool WithThreadId { get; set; } = true;

    /// <summary>
    /// Enrich with process ID
    /// </summary>
    public bool WithProcessId { get; set; } = true;

    /// <summary>
    /// Enrich with process name
    /// </summary>
    public bool WithProcessName { get; set; } = true;

    /// <summary>
    /// Enrich with exception details
    /// </summary>
    public bool WithExceptionDetails { get; set; } = true;

    /// <summary>
    /// Custom properties to enrich all logs
    /// </summary>
    public Dictionary<string, string> CustomProperties { get; set; } = new();
}
