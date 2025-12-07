using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Shouldly;
using LogManager.Configuration;
using LogManager.Enrichers;
using LogManager.Extensions;

namespace LogManager.Tests;

[TestFixture]
public class LogManagerExtensionsTests
{
    [Test]
    public void AddLogManager_WithConfiguration_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LogManager:ApplicationName"] = "TestApp",
                ["LogManager:Environment"] = "Test",
                ["LogManager:MinimumLevel"] = "Debug"
            })
            .Build();

        // Act
        services.AddLogManager(configuration, "LogManager");
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<LogManagerOptions>>();
        options.ShouldNotBeNull();
        options.Value.ApplicationName.ShouldBe("TestApp");
        options.Value.Environment.ShouldBe("Test");
        options.Value.MinimumLevel.ShouldBe("Debug");

        var correlationIdAccessor = serviceProvider.GetService<ICorrelationIdAccessor>();
        correlationIdAccessor.ShouldNotBeNull();
        correlationIdAccessor.ShouldBeOfType<DefaultCorrelationIdAccessor>();
    }

    [Test]
    public void AddLogManager_WithAction_ShouldConfigureOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogManager(opts =>
        {
            opts.ApplicationName = "ActionApp";
            opts.Environment = "Production";
            opts.MinimumLevelEnum = LogLevel.Warning;
        });
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<LogManagerOptions>>();
        options.ShouldNotBeNull();
        options.Value.ApplicationName.ShouldBe("ActionApp");
        options.Value.Environment.ShouldBe("Production");
        options.Value.MinimumLevelEnum.ShouldBe(LogLevel.Warning);

        var correlationIdAccessor = serviceProvider.GetService<ICorrelationIdAccessor>();
        correlationIdAccessor.ShouldNotBeNull();
    }

    [Test]
    public void AddLogManager_WithServiceProviderAccess_ShouldConfigureOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Register a custom options class that LogManager might need
        services.Configure<CustomAppOptions>(opts =>
        {
            opts.AppName = "CustomApp";
            opts.LogLevel = "Error";
        });

        // Act
        services.AddLogManager((opts, sp) =>
        {
            opts.ApplicationName = "ServiceAccessApp";
            opts.Environment = "Development";
            opts.MinimumLevelEnum = LogLevel.Information;
            
            // Verify service provider is passed and can resolve services
            sp.ShouldNotBeNull();
            var customOpts = sp.GetService<IOptions<CustomAppOptions>>();
            customOpts.ShouldNotBeNull();
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<LogManagerOptions>>();
        options.ShouldNotBeNull();
        options.Value.ApplicationName.ShouldBe("ServiceAccessApp");
        options.Value.Environment.ShouldBe("Development");
        options.Value.MinimumLevelEnum.ShouldBe(LogLevel.Information);

        var correlationIdAccessor = serviceProvider.GetService<ICorrelationIdAccessor>();
        correlationIdAccessor.ShouldNotBeNull();

        // Verify custom options are still accessible
        var customOptions = serviceProvider.GetService<IOptions<CustomAppOptions>>();
        customOptions.ShouldNotBeNull();
        customOptions.Value.AppName.ShouldBe("CustomApp");
    }

    [Test]
    public void AddLogManager_WithServiceProviderAccess_ShouldResolveOtherOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.Configure<CustomAppOptions>(opts =>
        {
            opts.AppName = "DynamicApp";
            opts.LogLevel = "Warning";
        });

        string? resolvedAppName = null;

        // Act
        services.AddLogManager((opts, sp) =>
        {
            var customOpts = sp.GetService<IOptions<CustomAppOptions>>()?.Value;
            if (customOpts != null)
            {
                resolvedAppName = customOpts.AppName;
                opts.ApplicationName = customOpts.AppName;
                opts.MinimumLevelEnum = customOpts.LogLevel == "Warning" 
                    ? LogLevel.Warning 
                    : LogLevel.Information;
            }
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<LogManagerOptions>>();
        options.ShouldNotBeNull();
        options.Value.ApplicationName.ShouldBe("DynamicApp");
        options.Value.MinimumLevelEnum.ShouldBe(LogLevel.Warning);
        resolvedAppName.ShouldBe("DynamicApp");
    }

    [Test]
    public void AddLogManager_WithServiceProviderAccess_ShouldPassServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        IServiceProvider? capturedProvider = null;

        // Act
        services.AddLogManager((opts, sp) =>
        {
            capturedProvider = sp;
            opts.ApplicationName = "TestApp";
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Force options to be resolved to trigger the configuration action
        var options = serviceProvider.GetRequiredService<IOptions<LogManagerOptions>>();
        var value = options.Value; // Access Value to trigger configuration

        // Assert
        capturedProvider.ShouldNotBeNull("Service provider should be passed to configuration action");
        value.ApplicationName.ShouldBe("TestApp");
    }

    [Test]
    public void AddLogManagerFilePath_ShouldOverrideFileLoggingPath()
    {
        // Arrange
        var services = new ServiceCollection();
        var testPath = "/custom/log/path";

        // Act
        services.AddLogManager(opts =>
        {
            opts.ApplicationName = "TestApp";
        });
        services.AddLogManagerFilePath(testPath);
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<LogManagerOptions>>();
        options.ShouldNotBeNull();
        options.Value.FileLogging.ShouldNotBeNull();
        options.Value.FileLogging.Enabled.ShouldBeTrue();
        options.Value.FileLogging.Path.ShouldBe(testPath);
    }

    [Test]
    public void AddLogManagerFilePath_ShouldInitializeFileLoggingIfNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var testPath = "/var/log/test";

        // Act
        services.AddLogManagerFilePath(testPath);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<LogManagerOptions>>();
        options.ShouldNotBeNull();
        options.Value.FileLogging.ShouldNotBeNull();
        options.Value.FileLogging.Enabled.ShouldBeTrue();
        options.Value.FileLogging.Path.ShouldBe(testPath);
    }

    [Test]
    public void CreateBootstrapLogger_ShouldCreateLogger()
    {
        // Act
        var logger = LogManagerExtensions.CreateBootstrapLogger("BootstrapApp", "Development");

        // Assert
        logger.ShouldNotBeNull();
        
        // Cleanup
        Serilog.Log.CloseAndFlush();
    }

    [Test]
    public void CreateBootstrapLogger_WithDefaults_ShouldUseDefaultValues()
    {
        // Act
        var logger = LogManagerExtensions.CreateBootstrapLogger();

        // Assert
        logger.ShouldNotBeNull();
        
        // Cleanup
        Serilog.Log.CloseAndFlush();
    }
}

// Test helper class
public class CustomAppOptions
{
    public string AppName { get; set; } = "DefaultApp";
    public string LogLevel { get; set; } = "Information";
}
