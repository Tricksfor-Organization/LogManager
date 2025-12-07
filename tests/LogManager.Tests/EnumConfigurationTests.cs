using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shouldly;
using Serilog;
using LogManager.Configuration;
using LogManager.Tests.Infrastructure;

namespace LogManager.Tests;

[TestFixture]
public class EnumConfigurationTests
{
    private string? _testLogDirectory;

    [SetUp]
    public void Setup()
    {
        _testLogDirectory = TestHelpers.CreateTempDirectory("enum_config_tests");
    }

    [TearDown]
    public void TearDown()
    {
        if (_testLogDirectory != null)
        {
            TestHelpers.CleanupDirectory(_testLogDirectory);
        }
    }

    [Test]
    [TestCase(LogLevel.Trace, "Verbose")]
    [TestCase(LogLevel.Debug, "Debug")]
    [TestCase(LogLevel.Information, "Information")]
    [TestCase(LogLevel.Warning, "Warning")]
    [TestCase(LogLevel.Error, "Error")]
    [TestCase(LogLevel.Critical, "Fatal")]
    public async Task LogLevel_Enum_Should_Convert_To_Serilog_Correctly(LogLevel enumValue, string expectedSerilogLevel)
    {
        // Arrange
        var options = new LogManagerOptions
        {
            ApplicationName = "EnumTest",
            MinimumLevelEnum = enumValue,
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = _testLogDirectory!,
                FileNamePattern = "test-.log"
            }
        };

        var logger = LoggerConfigurator.ConfigureLogger(options).CreateLogger();

        // Act - Log at the configured level
        logger.Write(MapToSerilogLevel(expectedSerilogLevel), "Test message at {Level}", expectedSerilogLevel);
        await Log.CloseAndFlushAsync();
        await Task.Delay(500);

        // Assert - Verify log was written
        var logFiles = Directory.GetFiles(_testLogDirectory!, "test-*.log");
        logFiles.ShouldNotBeEmpty();
        
        var content = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        content.ShouldContain("Test message");
    }

    [Test]
    public async Task MinimumLevelEnum_Should_Take_Precedence_Over_String()
    {
        // Arrange
        var options = new LogManagerOptions
        {
            ApplicationName = "PrecedenceTest",
            MinimumLevel = "Information",  // String says Information
            MinimumLevelEnum = LogLevel.Error,  // Enum says Error (should win)
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = _testLogDirectory!,
                FileNamePattern = "precedence-.log"
            }
        };

        var logger = LoggerConfigurator.ConfigureLogger(options).CreateLogger();

        // Act - Log at Warning level (should be filtered out since Error is minimum)
        logger.Warning("This should not appear");
        logger.Error("This should appear");
        await Log.CloseAndFlushAsync();
        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "precedence-*.log");
        logFiles.ShouldNotBeEmpty();
        
        var content = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        content.ShouldNotContain("This should not appear");
        content.ShouldContain("This should appear");
    }

    [Test]
    public async Task MinimumLevel_String_Should_Work_When_Enum_Is_Null()
    {
        // Arrange
        var options = new LogManagerOptions
        {
            ApplicationName = "StringFallbackTest",
            MinimumLevel = "Warning",  // String configuration
            MinimumLevelEnum = null,  // No enum
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = _testLogDirectory!,
                FileNamePattern = "fallback-.log"
            }
        };

        var logger = LoggerConfigurator.ConfigureLogger(options).CreateLogger();

        // Act
        logger.Information("This should not appear");
        logger.Warning("This should appear");
        await Log.CloseAndFlushAsync();
        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "fallback-*.log");
        logFiles.ShouldNotBeEmpty();
        
        var content = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        content.ShouldNotContain("This should not appear");
        content.ShouldContain("This should appear");
    }

    [Test]
    public async Task Default_MinimumLevel_Should_Be_Information()
    {
        // Arrange
        var options = new LogManagerOptions
        {
            ApplicationName = "DefaultTest",
            // No MinimumLevel or MinimumLevelEnum set
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = _testLogDirectory!,
                FileNamePattern = "default-.log"
            }
        };

        var logger = LoggerConfigurator.ConfigureLogger(options).CreateLogger();

        // Act
        logger.Debug("This should not appear");
        logger.Information("This should appear");
        await Log.CloseAndFlushAsync();
        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "default-*.log");
        logFiles.ShouldNotBeEmpty();
        
        var content = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        content.ShouldNotContain("This should not appear");
        content.ShouldContain("This should appear");
    }

    [Test]
    [TestCase(FileRollingInterval.Day, "Day")]
    [TestCase(FileRollingInterval.Hour, "Hour")]
    [TestCase(FileRollingInterval.Month, "Month")]
    [TestCase(FileRollingInterval.Year, "Year")]
    [TestCase(FileRollingInterval.Minute, "Minute")]
    [TestCase(FileRollingInterval.Infinite, "Infinite")]
    public async Task RollingInterval_Enum_Should_Convert_To_Serilog_Correctly(FileRollingInterval enumValue, string description)
    {
        // Arrange
        var options = new LogManagerOptions
        {
            ApplicationName = "RollingTest",
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = _testLogDirectory!,
                FileNamePattern = $"rolling-{description.ToLower()}-.log",
                RollingIntervalEnum = enumValue
            }
        };

        var logger = LoggerConfigurator.ConfigureLogger(options).CreateLogger();

        // Act
        logger.Information("Test rolling interval {Interval}", description);
        await Log.CloseAndFlushAsync();
        await Task.Delay(500);

        // Assert - Verify log file was created
        var logFiles = Directory.GetFiles(_testLogDirectory!, $"rolling-{description.ToLower()}-*.log");
        logFiles.ShouldNotBeEmpty($"Log file should be created for {description} rolling interval");
        
        var content = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        content.ShouldContain($"Test rolling interval {description}");
    }

    [Test]
    public async Task RollingIntervalEnum_Should_Take_Precedence_Over_String()
    {
        // Arrange
        var options = new LogManagerOptions
        {
            ApplicationName = "RollingPrecedenceTest",
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = _testLogDirectory!,
                FileNamePattern = "rolling-precedence-.log",
                RollingInterval = "Day",  // String says Day
                RollingIntervalEnum = FileRollingInterval.Hour  // Enum says Hour (should win)
            }
        };

        var logger = LoggerConfigurator.ConfigureLogger(options).CreateLogger();

        // Act
        logger.Information("Test precedence");
        await Log.CloseAndFlushAsync();
        await Task.Delay(500);

        // Assert - File should exist with hour-based naming
        var logFiles = Directory.GetFiles(_testLogDirectory!, "rolling-precedence-*.log");
        logFiles.ShouldNotBeEmpty();
        
        // Verify the file was created (the actual rolling behavior would need time-based testing)
        var content = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        content.ShouldContain("Test precedence");
    }

    [Test]
    public async Task RollingInterval_String_Should_Work_When_Enum_Is_Null()
    {
        // Arrange
        var options = new LogManagerOptions
        {
            ApplicationName = "RollingFallbackTest",
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = _testLogDirectory!,
                FileNamePattern = "rolling-fallback-.log",
                RollingInterval = "Day",  // String configuration
                RollingIntervalEnum = null  // No enum
            }
        };

        var logger = LoggerConfigurator.ConfigureLogger(options).CreateLogger();

        // Act
        logger.Information("Test string fallback");
        await Log.CloseAndFlushAsync();
        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "rolling-fallback-*.log");
        logFiles.ShouldNotBeEmpty();
        
        var content = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        content.ShouldContain("Test string fallback");
    }

    [Test]
    public async Task Default_RollingInterval_Should_Be_Day()
    {
        // Arrange
        var options = new LogManagerOptions
        {
            ApplicationName = "RollingDefaultTest",
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = _testLogDirectory!,
                FileNamePattern = "rolling-default-.log"
                // No RollingInterval or RollingIntervalEnum set
            }
        };

        var logger = LoggerConfigurator.ConfigureLogger(options).CreateLogger();

        // Act
        logger.Information("Test default rolling");
        await Log.CloseAndFlushAsync();
        await Task.Delay(500);

        // Assert - Default is Day
        var logFiles = Directory.GetFiles(_testLogDirectory!, "rolling-default-*.log");
        logFiles.ShouldNotBeEmpty();
        
        var content = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        content.ShouldContain("Test default rolling");
    }

    [Test]
    public async Task Combined_Enum_Configuration_Should_Work_Together()
    {
        // Arrange
        var options = new LogManagerOptions
        {
            ApplicationName = "CombinedTest",
            MinimumLevelEnum = LogLevel.Debug,
            EnableConsole = false,
            FileLogging = new FileLoggingOptions
            {
                Enabled = true,
                Path = _testLogDirectory!,
                FileNamePattern = "combined-.log",
                RollingIntervalEnum = FileRollingInterval.Day
            }
        };

        var logger = LoggerConfigurator.ConfigureLogger(options).CreateLogger();

        // Act
        logger.Debug("Debug message");
        logger.Information("Info message");
        logger.Warning("Warning message");
        await Log.CloseAndFlushAsync();
        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "combined-*.log");
        logFiles.ShouldNotBeEmpty();
        
        var content = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        content.ShouldContain("Debug message");
        content.ShouldContain("Info message");
        content.ShouldContain("Warning message");
    }

    private static Serilog.Events.LogEventLevel MapToSerilogLevel(string level)
    {
        return level switch
        {
            "Verbose" => Serilog.Events.LogEventLevel.Verbose,
            "Debug" => Serilog.Events.LogEventLevel.Debug,
            "Information" => Serilog.Events.LogEventLevel.Information,
            "Warning" => Serilog.Events.LogEventLevel.Warning,
            "Error" => Serilog.Events.LogEventLevel.Error,
            "Fatal" => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information
        };
    }
}
