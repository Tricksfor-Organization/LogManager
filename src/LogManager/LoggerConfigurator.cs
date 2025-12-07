using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Formatting.Elasticsearch;
using Serilog.Sinks.Elasticsearch;
using Serilog.Sinks.Grafana.Loki;
using LogManager.Configuration;
using LogManager.Enrichers;

namespace LogManager;

/// <summary>
/// Main configurator for LogManager with support for multiple sinks
/// </summary>
public static class LoggerConfigurator
{
    /// <summary>
    /// Configure Serilog logger with multiple sinks based on options
    /// </summary>
    public static LoggerConfiguration ConfigureLogger(
        LogManagerOptions options,
        ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        var loggerConfig = new LoggerConfiguration();

        // Set minimum level - prefer enum if provided
        var minLevel = options.MinimumLevelEnum.HasValue 
            ? ConvertLogLevel(options.MinimumLevelEnum.Value)
            : ParseLogLevel(options.MinimumLevel);
        loggerConfig.MinimumLevel.Is(minLevel);

        // Apply level overrides
        foreach (var @override in options.MinimumLevelOverrides)
        {
            loggerConfig.MinimumLevel.Override(@override.Key, ParseLogLevel(@override.Value));
        }

        // Configure enrichers
        ConfigureEnrichers(loggerConfig, options, correlationIdAccessor);

        // Configure sinks
        ConfigureSinks(loggerConfig, options);

        return loggerConfig;
    }

    /// <summary>
    /// Configure logger from IConfiguration
    /// </summary>
    public static LoggerConfiguration ConfigureLogger(
        IConfiguration configuration,
        string sectionName = "LogManager",
        ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        var options = new LogManagerOptions();
        configuration.GetSection(sectionName).Bind(options);

        return ConfigureLogger(options, correlationIdAccessor);
    }

    private static void ConfigureEnrichers(
        LoggerConfiguration loggerConfig,
        LogManagerOptions options,
        ICorrelationIdAccessor? correlationIdAccessor)
    {
        // Standard enrichers
        loggerConfig.Enrich.FromLogContext();

        if (options.Enrichment.WithMachineName)
        {
            loggerConfig.Enrich.WithMachineName();
        }

        if (options.Enrichment.WithEnvironmentName)
        {
            loggerConfig.Enrich.WithProperty("Environment", options.Environment);
        }

        if (options.Enrichment.WithThreadId)
        {
            loggerConfig.Enrich.WithThreadId();
        }

        if (options.Enrichment.WithProcessId)
        {
            loggerConfig.Enrich.WithProcessId();
        }

        if (options.Enrichment.WithProcessName)
        {
            loggerConfig.Enrich.WithProcessName();
        }

        if (options.Enrichment.WithExceptionDetails)
        {
            loggerConfig.Enrich.WithExceptionDetails();
        }

        // Service context enricher
        var version = GetServiceVersion();
        loggerConfig.Enrich.With(new ServiceContextEnricher(
            options.ApplicationName,
            version,
            Environment.GetEnvironmentVariable("SERVICE_ID")
        ));

        // Container enricher for Docker/Kubernetes
        loggerConfig.Enrich.With(new ContainerEnricher());

        // Correlation ID enricher
        if (correlationIdAccessor != null)
        {
            loggerConfig.Enrich.With(new CorrelationIdEnricher(correlationIdAccessor));
        }

        // Custom properties
        foreach (var property in options.Enrichment.CustomProperties)
        {
            loggerConfig.Enrich.WithProperty(property.Key, property.Value);
        }
    }

    private static void ConfigureSinks(LoggerConfiguration loggerConfig, LogManagerOptions options)
    {
        // Console sink
        if (options.EnableConsole)
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] {Message:lj}{NewLine}{Exception}"
            );
        }

        // File sink with daily rotation
        if (options.FileLogging?.Enabled == true)
        {
            ConfigureFileSink(loggerConfig, options.FileLogging);
        }

        // Elasticsearch sink
        if (options.Elasticsearch?.Enabled == true)
        {
            ConfigureElasticsearchSink(loggerConfig, options.Elasticsearch, options.ApplicationName);
        }

        // Loki sink
        if (options.Loki?.Enabled == true)
        {
            ConfigureLokiSink(loggerConfig, options.Loki, options.ApplicationName, options.Environment);
        }
    }

    private static void ConfigureFileSink(
        LoggerConfiguration loggerConfig,
        FileLoggingOptions fileOptions)
    {
        var logPath = ExpandPath(fileOptions.Path);
        // Prefer enum if provided, otherwise parse string
        var rollingInterval = fileOptions.RollingIntervalEnum.HasValue
            ? ConvertRollingInterval(fileOptions.RollingIntervalEnum.Value)
            : ParseRollingInterval(fileOptions.RollingInterval);
        var fullPath = Path.IsPathRooted(fileOptions.FileNamePattern)
            ? fileOptions.FileNamePattern
            : Path.Combine(logPath, fileOptions.FileNamePattern);

        loggerConfig.WriteTo.File(
            path: fullPath,
            rollingInterval: rollingInterval,
            retainedFileCountLimit: fileOptions.RetainedFileCountLimit,
            fileSizeLimitBytes: fileOptions.FileSizeLimitBytes,
            rollOnFileSizeLimit: fileOptions.RollOnFileSizeLimit,
            shared: fileOptions.Shared,
            buffered: fileOptions.Buffered,
            outputTemplate: fileOptions.OutputTemplate
        );
    }

    /// <summary>
    /// Configures Elasticsearch sink with the specified options.
    /// </summary>
    /// <remarks>
    /// The IndexFormat in ElasticsearchOptions must use double braces for the date token 
    /// (e.g., "logs-myapp-{{0:yyyy.MM.dd}}") because the application name is injected 
    /// via string.Format first, then Serilog uses the resulting format for the date.
    /// </remarks>
    private static void ConfigureElasticsearchSink(
        LoggerConfiguration loggerConfig,
        ElasticsearchOptions esOptions,
        string applicationName)
    {
        var elasticsearchSinkOptions = new ElasticsearchSinkOptions(
            esOptions.NodeUris.Select(uri => new Uri(uri)))
        {
            AutoRegisterTemplate = esOptions.AutoRegisterTemplate,
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
            IndexFormat = string.Format(esOptions.IndexFormat, applicationName.ToLowerInvariant()),
            BatchPostingLimit = esOptions.BatchPostingLimit,
            Period = esOptions.Period,
            NumberOfShards = esOptions.NumberOfShards,
            NumberOfReplicas = esOptions.NumberOfReplicas,
            EmitEventFailure = esOptions.EmitEventFailure 
                ? EmitEventFailureHandling.WriteToSelfLog 
                : EmitEventFailureHandling.ThrowException,
            ConnectionTimeout = esOptions.ConnectionTimeout,
            ModifyConnectionSettings = conn =>
            {
                if (!string.IsNullOrEmpty(esOptions.Username) && !string.IsNullOrEmpty(esOptions.Password))
                {
                    conn = conn.BasicAuthentication(esOptions.Username, esOptions.Password);
                }
                else if (!string.IsNullOrEmpty(esOptions.ApiKey))
                {
                    using (var credentials = new Elasticsearch.Net.ApiKeyAuthenticationCredentials(esOptions.ApiKey))
                    {
                        conn = conn.ApiKeyAuthentication(credentials);
                    }
                }

                return conn;
            },
            CustomFormatter = new ElasticsearchJsonFormatter(
                inlineFields: true,
                renderMessageTemplate: true,
                formatProvider: null
            )
        };

        loggerConfig.WriteTo.Elasticsearch(elasticsearchSinkOptions);
    }

    private static void ConfigureLokiSink(
        LoggerConfiguration loggerConfig,
        LokiOptions lokiOptions,
        string applicationName,
        string environment)
    {
        var labels = new List<LokiLabel>
        {
            new() { Key = "app", Value = applicationName.ToLowerInvariant() },
            new() { Key = "environment", Value = environment.ToLowerInvariant() }
        };

        // Add custom labels
        foreach (var label in lokiOptions.Labels)
        {
            labels.Add(new LokiLabel { Key = label.Key, Value = label.Value });
        }

        var credentials = lokiOptions.Username != null && lokiOptions.Password != null
            ? new LokiCredentials { Login = lokiOptions.Username, Password = lokiOptions.Password }
            : null;

        loggerConfig.WriteTo.GrafanaLoki(
            uri: lokiOptions.Url,
            labels: labels,
            credentials: credentials,
            textFormatter: new Serilog.Formatting.Display.MessageTemplateTextFormatter(lokiOptions.OutputTemplate),
            batchPostingLimit: lokiOptions.BatchPostingLimit,
            period: lokiOptions.Period,
            queueLimit: lokiOptions.QueueLimit,
            tenant: lokiOptions.TenantId
        );
    }

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    private static LogEventLevel ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel level)
    {
        return level switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => LogEventLevel.Verbose,
            Microsoft.Extensions.Logging.LogLevel.Debug => LogEventLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => LogEventLevel.Information,
            Microsoft.Extensions.Logging.LogLevel.Warning => LogEventLevel.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => LogEventLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => LogEventLevel.Fatal,
            Microsoft.Extensions.Logging.LogLevel.None => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    private static Serilog.RollingInterval ParseRollingInterval(string interval)
    {
        return interval.ToLowerInvariant() switch
        {
            "infinite" => Serilog.RollingInterval.Infinite,
            "year" => Serilog.RollingInterval.Year,
            "month" => Serilog.RollingInterval.Month,
            "day" => Serilog.RollingInterval.Day,
            "hour" => Serilog.RollingInterval.Hour,
            "minute" => Serilog.RollingInterval.Minute,
            _ => Serilog.RollingInterval.Day
        };
    }

    private static Serilog.RollingInterval ConvertRollingInterval(Configuration.FileRollingInterval interval)
    {
        return interval switch
        {
            Configuration.FileRollingInterval.Infinite => Serilog.RollingInterval.Infinite,
            Configuration.FileRollingInterval.Year => Serilog.RollingInterval.Year,
            Configuration.FileRollingInterval.Month => Serilog.RollingInterval.Month,
            Configuration.FileRollingInterval.Day => Serilog.RollingInterval.Day,
            Configuration.FileRollingInterval.Hour => Serilog.RollingInterval.Hour,
            Configuration.FileRollingInterval.Minute => Serilog.RollingInterval.Minute,
            _ => Serilog.RollingInterval.Day
        };
    }

    private static string ExpandPath(string path)
    {
        // Expand environment variables
        path = Environment.ExpandEnvironmentVariables(path);

        // Handle relative paths by combining with base directory
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }

        // Normalize and resolve the path
        path = Path.GetFullPath(path);

        // Ensure directory exists (treat input as a directory path)
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return path;
    }

    private static string GetServiceVersion()
    {
        var version = Environment.GetEnvironmentVariable("SERVICE_VERSION");
        if (!string.IsNullOrEmpty(version))
        {
            return version;
        }

        // Try to get from assembly
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        return assembly?.GetName().Version?.ToString() ?? "1.0.0";
    }
}
