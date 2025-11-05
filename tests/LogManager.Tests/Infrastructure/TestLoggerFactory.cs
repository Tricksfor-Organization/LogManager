using Serilog;
using LogManager.Configuration;
using LogManager.Enrichers;

namespace LogManager.Tests.Infrastructure;

/// <summary>
/// Factory for creating test loggers with various configurations
/// </summary>
public static class TestLoggerFactory
{
    /// <summary>
    /// Create a logger with file sink for testing
    /// </summary>
    public static ILogger CreateFileLogger(string logDirectory, string applicationName = "TestApp")
    {
        var options = new LogManagerOptions
        {
            ApplicationName = applicationName,
            Environment = "Test",
            MinimumLevel = "Debug",
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = logDirectory,
                FileNamePattern = "test-.txt",
                RollingInterval = "Day",
                RetainedFileCountLimit = 5,
                FileSizeLimitBytes = 10 * 1024 * 1024, // 10 MB
                Shared = true,
                Buffered = false // Immediate flush for testing
            }
        };

        return LoggerConfigurator
            .ConfigureLogger(options, new DefaultCorrelationIdAccessor())
            .CreateLogger();
    }

    /// <summary>
    /// Create a logger with Elasticsearch sink for testing
    /// </summary>
    public static ILogger CreateElasticsearchLogger(
        string elasticsearchUrl,
        string applicationName = "TestApp")
    {
        var options = new LogManagerOptions
        {
            ApplicationName = applicationName,
            Environment = "Test",
            MinimumLevel = "Debug",
            EnableConsole = false,
            Elasticsearch = new ElasticsearchOptions
            {
                Enabled = true,
                NodeUris = new List<string> { elasticsearchUrl },
                IndexFormat = $"test-logs-{applicationName.ToLowerInvariant()}-{{0:yyyy.MM.dd}}",
                AutoRegisterTemplate = true,
                BatchPostingLimit = 1, // Immediate for testing
                Period = TimeSpan.FromMilliseconds(100),
                EmitEventFailure = true
            }
        };

        return LoggerConfigurator
            .ConfigureLogger(options, new DefaultCorrelationIdAccessor())
            .CreateLogger();
    }

    /// <summary>
    /// Create a logger with Loki sink for testing
    /// </summary>
    public static ILogger CreateLokiLogger(
        string lokiUrl,
        string applicationName = "TestApp")
    {
        var options = new LogManagerOptions
        {
            ApplicationName = applicationName,
            Environment = "Test",
            MinimumLevel = "Debug",
            EnableConsole = false,
            Loki = new LokiOptions
            {
                Enabled = true,
                Url = lokiUrl,
                Labels = new Dictionary<string, string>(), // Don't duplicate app/environment, they're added automatically
                BatchPostingLimit = 1, // Immediate for testing
                Period = TimeSpan.FromMilliseconds(100)
            }
        };

        return LoggerConfigurator
            .ConfigureLogger(options, new DefaultCorrelationIdAccessor())
            .CreateLogger();
    }
}
