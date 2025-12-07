namespace LogManager.Configuration;

/// <summary>
/// Configuration for file-based logging with daily rotation
/// </summary>
public class FileLoggingOptions
{
    /// <summary>
    /// Enable file logging
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Path to log directory (supports environment variables and Docker volumes)
    /// </summary>
    public string Path { get; set; } = "/var/log/app";

    /// <summary>
    /// Log file name pattern (e.g., "log-.txt" will produce "log-20250105.txt")
    /// </summary>
    public string FileNamePattern { get; set; } = "log-.txt";

    /// <summary>
    /// Rolling interval (Day, Hour, Month)
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// Rolling interval as enum (preferred for code-based configuration)
    /// </summary>
    public RollingInterval? RollingIntervalEnum { get; set; }

    /// <summary>
    /// Number of days to retain log files (0 = unlimited)
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 31;

    /// <summary>
    /// Maximum file size in bytes before rolling (null = unlimited)
    /// </summary>
    public long? FileSizeLimitBytes { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Rollover on file size limit
    /// </summary>
    public bool RollOnFileSizeLimit { get; set; } = true;

    /// <summary>
    /// Use shared file access (useful for Docker containers)
    /// </summary>
    public bool Shared { get; set; } = true;

    /// <summary>
    /// Buffer size for file writes
    /// </summary>
    public bool Buffered { get; set; } = true;

    /// <summary>
    /// Output template for file logs
    /// </summary>
    public string OutputTemplate { get; set; } = 
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";
}
