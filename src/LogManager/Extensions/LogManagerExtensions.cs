using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using LogManager.Configuration;
using LogManager.Enrichers;

namespace LogManager.Extensions;

/// <summary>
/// Extension methods for configuring LogManager in .NET applications
/// </summary>
public static class LogManagerExtensions
{
    /// <summary>
    /// Add LogManager to the service collection
    /// </summary>
    public static IServiceCollection AddLogManager(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "LogManager")
    {
        // Register options
        services.Configure<LogManagerOptions>(configuration.GetSection(sectionName));

        // Register correlation ID accessor
        services.AddSingleton<ICorrelationIdAccessor, DefaultCorrelationIdAccessor>();

        return services;
    }

    /// <summary>
    /// Add LogManager with custom options
    /// </summary>
    public static IServiceCollection AddLogManager(
        this IServiceCollection services,
        Action<LogManagerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<ICorrelationIdAccessor, DefaultCorrelationIdAccessor>();

        return services;
    }

    /// <summary>
    /// Add LogManager with custom options and optional access to service collection
    /// </summary>
    public static IServiceCollection AddLogManager(
        this IServiceCollection services,
        Action<LogManagerOptions, IServiceCollection?> configureOptions)
    {
        services.Configure<LogManagerOptions>(opts => configureOptions(opts, services));
        services.AddSingleton<ICorrelationIdAccessor, DefaultCorrelationIdAccessor>();

        return services;
    }

    /// <summary>
    /// Convenience helper to override the log file path via DI registration.
    /// </summary>
    public static IServiceCollection AddLogManagerFilePath(
        this IServiceCollection services,
        string logDirectoryPath)
    {
        // Ensure FileLogging is initialized and override the path
        services.PostConfigure<LogManagerOptions>(opts =>
        {
            opts.FileLogging ??= new FileLoggingOptions();
            opts.FileLogging.Enabled = true;
            opts.FileLogging.Path = logDirectoryPath;
        });

        return services;
    }

    /// <summary>
    /// Configure Serilog for the host using LogManager
    /// </summary>
    public static IHostBuilder UseLogManager(
        this IHostBuilder hostBuilder,
        string sectionName = "LogManager")
    {
        return hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            var options = new LogManagerOptions();
            context.Configuration.GetSection(sectionName).Bind(options);
            
            var correlationIdAccessor = services.GetService<ICorrelationIdAccessor>();
            
            // Configure the logger directly
            ConfigureLoggerConfiguration(loggerConfiguration, options, correlationIdAccessor);
        });
    }

    /// <summary>
    /// Configure Serilog with custom options
    /// </summary>
    public static IHostBuilder UseLogManager(
        this IHostBuilder hostBuilder,
        Action<LogManagerOptions> configureOptions)
    {
        return hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            var options = new LogManagerOptions();
            configureOptions(options);

            var correlationIdAccessor = services.GetService<ICorrelationIdAccessor>();
            
            // Configure the logger directly
            ConfigureLoggerConfiguration(loggerConfiguration, options, correlationIdAccessor);
        });
    }

    private static void ConfigureLoggerConfiguration(
        LoggerConfiguration loggerConfiguration,
        LogManagerOptions options,
        ICorrelationIdAccessor? correlationIdAccessor)
    {
        // Get the configured logger and apply it
        var tempConfig = LoggerConfigurator.ConfigureLogger(options, correlationIdAccessor);
        var tempLogger = tempConfig.CreateLogger();
        
        // Write to the sub logger
        loggerConfiguration.WriteTo.Logger(tempLogger);
    }

    /// <summary>
    /// Create a bootstrap logger for early initialization logging
    /// </summary>
    public static ILogger CreateBootstrapLogger(
        string applicationName = "BootstrapApp",
        string environment = "Development")
    {
        var options = new LogManagerOptions
        {
            ApplicationName = applicationName,
            Environment = environment,
            EnableConsole = true,
            MinimumLevel = "Information"
        };

        return LoggerConfigurator
            .ConfigureLogger(options)
            .CreateBootstrapLogger();
    }
}
