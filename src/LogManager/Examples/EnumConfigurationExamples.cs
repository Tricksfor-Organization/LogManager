using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using LogManager.Configuration;
using LogManager.Extensions;

namespace LogManager.Examples;

/// <summary>
/// Examples demonstrating how to use the new enum-based configuration with optional service collection access
/// </summary>
public static class EnumConfigurationExamples
{
    /// <summary>
    /// Example 1: Using enums for cleaner configuration
    /// </summary>
    public static void ConfigureWithEnums()
    {
        var services = new ServiceCollection();

        // Using the new enum-based configuration - more IDE-friendly and type-safe
        services.AddLogManager(opts =>
        {
            opts.ApplicationName = "MyApp";
            opts.Environment = "Production";
            
            // Using enum for minimum level - no string magic!
            opts.MinimumLevelEnum = LogLevel.Warning;
            
            // Configure file logging with enum
            opts.FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = "/var/log/myapp",
                RollingIntervalEnum = RollingInterval.Hour  // Type-safe enum!
            };
        });
    }

    /// <summary>
    /// Example 2: Using the new overload with access to IServiceCollection
    /// This allows you to fetch other registered services or options during configuration
    /// </summary>
    public static void ConfigureWithServiceAccess()
    {
        var services = new ServiceCollection();

        // Register some custom options that LogManager needs to read
        services.Configure<MyAppOptions>(opts =>
        {
            opts.LogLevel = "Debug";
            opts.LogPath = "/custom/path";
        });

        // Using the new overload that provides access to IServiceCollection
        services.AddLogManager((opts, serviceCollection) =>
        {
            opts.ApplicationName = "MyApp";
            opts.Environment = "Development";

            // Access other registered services/options
            if (serviceCollection != null)
            {
                using var tempProvider = serviceCollection.BuildServiceProvider();
                var myAppOptions = tempProvider.GetService<IOptions<MyAppOptions>>()?.Value;

                if (myAppOptions != null)
                {
                    // Use values from other options
                    opts.MinimumLevelEnum = myAppOptions.LogLevel switch
                    {
                        "Debug" => LogLevel.Debug,
                        "Warning" => LogLevel.Warning,
                        "Error" => LogLevel.Error,
                        _ => LogLevel.Information
                    };

                    opts.FileLogging = new FileLoggingOptions
                    {
                        Enabled = true,
                        Path = myAppOptions.LogPath,
                        RollingIntervalEnum = RollingInterval.Day
                    };
                }
            }
        });
    }

    /// <summary>
    /// Example 3: Backward compatibility - string-based configuration still works
    /// </summary>
    public static void ConfigureWithStrings()
    {
        var services = new ServiceCollection();

        // The old way still works! (for backward compatibility)
        services.AddLogManager(opts =>
        {
            opts.ApplicationName = "MyApp";
            opts.MinimumLevel = "Information";  // String still works
            
            opts.FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = "/var/log/myapp",
                RollingInterval = "Day"  // String still works
            };
        });
    }

    /// <summary>
    /// Example 4: Configuration from appsettings.json remains unchanged
    /// </summary>
    public static void ConfigureFromAppSettings(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        
        // This approach is unchanged and continues to work as before
        services.AddLogManager(configuration, "LogManager");
        
        // The IConfiguration-based overload remains exactly the same
    }
}

// Supporting class for example
public class MyAppOptions
{
    public string LogLevel { get; set; } = "Information";
    public string LogPath { get; set; } = "/var/log/app";
}
