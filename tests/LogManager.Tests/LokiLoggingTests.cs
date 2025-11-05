using FluentAssertions;
using NUnit.Framework;
using Serilog;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using LogManager.Tests.Infrastructure;

namespace LogManager.Tests;

[TestFixture]
public class LokiLoggingTests
{
    private IContainer? _lokiContainer;
    private string? _lokiUrl;
    private HttpClient? _httpClient;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        // Start Loki container
        _lokiContainer = new ContainerBuilder()
            .WithImage("grafana/loki:2.9.3")
            .WithPortBinding(3100, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r
                .ForPort(3100)
                .ForPath("/ready")))
            .Build();

        await _lokiContainer.StartAsync();
        
        var port = _lokiContainer.GetMappedPublicPort(3100);
        _lokiUrl = $"http://localhost:{port}";
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_lokiUrl)
        };

        // Wait for Loki to be ready
        await WaitForLokiAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _httpClient?.Dispose();
        
        if (_lokiContainer != null)
        {
            await _lokiContainer.DisposeAsync();
        }
    }

    [Test]
    public async Task Should_Send_Logs_To_Loki()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateLokiLogger(_lokiUrl!, "LokiTest");
        var testMessage = $"Test log message {Guid.NewGuid()}";

        // Act
        logger.Information(testMessage);
        await Log.CloseAndFlushAsync();

        // Wait for Loki to ingest
        await Task.Delay(3000);

        // Assert
        var logs = await QueryLokiAsync("{app=\"lokitest\"}");
        logs.Should().NotBeNull();
        logs.Should().Contain(testMessage);
    }

    [Test]
    public async Task Should_Include_Structured_Properties_In_Loki()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateLokiLogger(_lokiUrl!, "LokiStructured");
        var orderId = Guid.NewGuid().ToString();
        var customerId = 99999;

        // Act
        logger.Information("Processing order {OrderId} for customer {CustomerId}", orderId, customerId);
        await Log.CloseAndFlushAsync();

        await Task.Delay(3000);

        // Assert
        var logs = await QueryLokiAsync("{app=\"lokistructured\"}");
        logs.Should().Contain(orderId);
        logs.Should().Contain(customerId.ToString());
    }

    [Test]
    public async Task Should_Store_Exception_In_Loki()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateLokiLogger(_lokiUrl!, "LokiException");
        var exceptionMessage = $"Test exception {Guid.NewGuid()}";

        // Act
        try
        {
            throw new InvalidOperationException(exceptionMessage);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred");
        }
        await Log.CloseAndFlushAsync();

        await Task.Delay(3000);

        // Assert
        var logs = await QueryLokiAsync("{app=\"lokiexception\"}");
        logs.Should().Contain("InvalidOperationException");
        logs.Should().Contain(exceptionMessage);
    }

    [Test]
    public async Task Should_Apply_Custom_Labels_In_Loki()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateLokiLogger(_lokiUrl!, "LokiLabels");
        var testMessage = $"Label test {Guid.NewGuid()}";

        // Act
        logger.Information(testMessage);
        await Log.CloseAndFlushAsync();

        await Task.Delay(3000);

        // Assert
        // Query with environment label
        var logs = await QueryLokiAsync("{app=\"lokilabels\",environment=\"test\"}");
        logs.Should().Contain(testMessage);
    }

    [Test]
    public async Task Should_Handle_Different_Log_Levels_In_Loki()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateLokiLogger(_lokiUrl!, "LokiLevels");
        var correlationId = Guid.NewGuid().ToString();

        // Act
        logger.Debug("Debug message {CorrelationId}", correlationId);
        logger.Information("Info message {CorrelationId}", correlationId);
        logger.Warning("Warning message {CorrelationId}", correlationId);
        logger.Error("Error message {CorrelationId}", correlationId);
        await Log.CloseAndFlushAsync();

        await Task.Delay(3000);

        // Assert
        var logs = await QueryLokiAsync("{app=\"lokilevels\"}");
        var hasDebug = logs.Contains("DBG") || logs.Contains("Debug");
        var hasInfo = logs.Contains("INF") || logs.Contains("Information");
        var hasWarning = logs.Contains("WRN") || logs.Contains("Warning");
        var hasError = logs.Contains("ERR") || logs.Contains("Error");
        
        hasDebug.Should().BeTrue("should contain debug level");
        hasInfo.Should().BeTrue("should contain info level");
        hasWarning.Should().BeTrue("should contain warning level");
        hasError.Should().BeTrue("should contain error level");
    }

    [Test]
    public async Task Should_Support_High_Volume_Logging_To_Loki()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateLokiLogger(_lokiUrl!, "LokiVolume");
        var batchId = Guid.NewGuid().ToString();

        // Act
        for (int i = 0; i < 100; i++)
        {
            logger.Information("Batch {BatchId} entry {EntryNumber}", batchId, i);
        }
        await Log.CloseAndFlushAsync();

        await Task.Delay(5000);

        // Assert
        var logs = await QueryLokiAsync("{app=\"lokivolume\"}");
        logs.Should().Contain(batchId);
        // At least some entries should be present
        logs.Should().Contain("entry");
    }

    [Test]
    public async Task Should_Include_Service_Context_In_Loki()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateLokiLogger(_lokiUrl!, "LokiEnrich");
        var testMessage = $"Enrichment test {Guid.NewGuid()}";

        // Act
        logger.Information(testMessage);
        await Log.CloseAndFlushAsync();

        await Task.Delay(3000);

        // Assert
        var logs = await QueryLokiAsync("{app=\"lokienrich\"}");
        logs.Should().Contain(testMessage);
        // ServiceName should be in the log output
        var hasServiceContext = logs.Contains("ServiceName") || logs.Contains("LokiEnrich");
        hasServiceContext.Should().BeTrue("should contain service context information");
    }

    private async Task<string> QueryLokiAsync(string query)
    {
        try
        {
            var end = DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 1_000_000_000;
            var start = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds() * 1_000_000_000;
            
            var queryUrl = $"/loki/api/v1/query_range?query={Uri.EscapeDataString(query)}&start={start}&end={end}&limit=1000";
            
            var response = await _httpClient!.GetAsync(queryUrl);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Failed to query Loki: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task WaitForLokiAsync()
    {
        var maxAttempts = 30;
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var response = await _httpClient!.GetAsync("/ready");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(1000);
        }

        throw new Exception("Loki did not become ready in time");
    }
}
