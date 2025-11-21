using Shouldly;
using NUnit.Framework;
using Serilog;
using LogManager.Tests.Infrastructure;

namespace LogManager.Tests;

[TestFixture]
public class FileLoggingTests
{
    private string? _testLogDirectory;

    [SetUp]
    public void Setup()
    {
        _testLogDirectory = TestHelpers.CreateTempDirectory("file_log_tests");
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
    public async Task Should_Create_Log_File_When_Logging()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateFileLogger(_testLogDirectory!, "FileTest");

        // Act
        logger.Information("Test log message");
        logger.Information("Another message with {Property}", "value");
        await Log.CloseAndFlushAsync();

        // Wait for file to be written
        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "test-*.txt");
        logFiles.ShouldNotBeEmpty("log files should be created");

        var logContent = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);
        logContent.ShouldContain("Test log message");
        logContent.ShouldContain("Another message with");
    }

    [Test]
    public async Task Should_Include_Structured_Properties_In_Log()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateFileLogger(_testLogDirectory!, "StructuredTest");
        var testOrder = new { OrderId = 123, CustomerId = 456, Amount = 99.99 };

        // Act
        logger.Information("Processing order {OrderId} for customer {CustomerId} with amount {Amount:C}",
            testOrder.OrderId, testOrder.CustomerId, testOrder.Amount);
        await Log.CloseAndFlushAsync();

        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "test-*.txt");
        var logContent = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);

        // The values should appear in the message
        logContent.ShouldContain("123");
        logContent.ShouldContain("456");
        logContent.ShouldContain("Processing order");
    }

    [Test]
    public async Task Should_Log_Exceptions_With_Stack_Trace()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateFileLogger(_testLogDirectory!, "ExceptionTest");

        // Act
        try
        {
            throw new InvalidOperationException("Test exception message");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred while processing");
        }
        await Log.CloseAndFlushAsync();

        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "test-*.txt");
        var logContent = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);

        logContent.ShouldContain("InvalidOperationException");
        logContent.ShouldContain("Test exception message");
        logContent.ShouldContain("An error occurred while processing");
    }

    [Test]
    public async Task Should_Include_Service_Context_Enrichment()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateFileLogger(_testLogDirectory!, "EnrichmentTest");

        // Act
        logger.Information("Testing enrichment");
        await Log.CloseAndFlushAsync();

        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "test-*.txt");
        var logContent = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);

        // The message should be logged
        logContent.ShouldContain("Testing enrichment");
        // File should not be empty
        logContent.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Should_Create_Multiple_Log_Files_Within_Size_Limit()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateFileLogger(_testLogDirectory!, "RollingTest");

        // Act - Write many log entries to trigger rolling
        for (int i = 0; i < 1000; i++)
        {
            logger.Information("Log entry number {EntryNumber} with some additional text to make it larger", i);
        }
        await Log.CloseAndFlushAsync();

        await Task.Delay(1000);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "test-*.txt");
        logFiles.ShouldNotBeEmpty("at least one log file should exist");
    }

    [Test]
    public async Task Should_Handle_Relative_Path()
    {
        // Arrange
        var relativePath = Path.Combine(".", "logs", "test");
        var absolutePath = Path.GetFullPath(relativePath);
        Directory.CreateDirectory(absolutePath);

        try
        {
            // Act
            var logger = TestLoggerFactory.CreateFileLogger(relativePath, "RelativePathTest");
            logger.Information("Testing relative path");
            await Log.CloseAndFlushAsync();

            // Assert
            Directory.Exists(absolutePath).ShouldBeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(absolutePath))
            {
                Directory.Delete(absolutePath, recursive: true);
            }
        }
    }

    [Test]
    public async Task Should_Support_Different_Log_Levels()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateFileLogger(_testLogDirectory!, "LogLevelTest");

        // Act
        logger.Verbose("Verbose message");
        logger.Debug("Debug message");
        logger.Information("Information message");
        logger.Warning("Warning message");
        logger.Error("Error message");
        logger.Fatal("Fatal message");
        await Log.CloseAndFlushAsync();

        await Task.Delay(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory!, "test-*.txt");
        var logContent = await TestHelpers.ReadFileWithRetryAsync(logFiles[0]);

        logContent.ShouldContain("Debug message");
        logContent.ShouldContain("Information message");
        logContent.ShouldContain("Warning message");
        logContent.ShouldContain("Error message");
        logContent.ShouldContain("Fatal message");
    }
}
