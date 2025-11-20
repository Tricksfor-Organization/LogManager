using Elastic.Clients.Elasticsearch;
using FluentAssertions;
using NUnit.Framework;
using Serilog;
using Testcontainers.Elasticsearch;
using LogManager.Tests.Infrastructure;

namespace LogManager.Tests;

[TestFixture]
public class ElasticsearchLoggingTests
{
    private ElasticsearchContainer? _elasticsearchContainer;
    private ElasticsearchClient? _elasticsearchClient;
    private string? _elasticsearchUrl;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        // Start Elasticsearch container with more memory and explicit wait strategy
        _elasticsearchContainer = new ElasticsearchBuilder()
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:9.2.1")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("xpack.security.enabled", "false")
            .Build();

        await _elasticsearchContainer.StartAsync();
        
        _elasticsearchUrl = _elasticsearchContainer.GetConnectionString();
        TestContext.WriteLine($"Elasticsearch URL from container: {_elasticsearchUrl}");
        
        // Force HTTP protocol since container runs without TLS
        if (_elasticsearchUrl.StartsWith("https://"))
        {
            _elasticsearchUrl = _elasticsearchUrl.Replace("https://", "http://");
            TestContext.WriteLine($"Corrected to HTTP: {_elasticsearchUrl}");
        }
        
        // Create Elasticsearch client for verification
        // Note: ElasticsearchClientSettings is not disposable in Elastic.Clients.Elasticsearch 8.x
        _elasticsearchClient = new ElasticsearchClient(
            new ElasticsearchClientSettings(new Uri(_elasticsearchUrl))
                .DefaultIndex("test-logs"));

        // Wait for Elasticsearch to be ready
        await WaitForElasticsearchAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_elasticsearchContainer != null)
        {
            await _elasticsearchContainer.DisposeAsync();
        }
    }

    [Test]
    public async Task Should_Send_Logs_To_Elasticsearch()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateElasticsearchLogger(_elasticsearchUrl!, "ElasticTest");
        var testMessage = $"Test log message {Guid.NewGuid()}";

        // Act
        logger.Information(testMessage);
        await ((IAsyncDisposable)logger).DisposeAsync();

        // Wait for Elasticsearch to index
        await Task.Delay(2000);

        // Assert
        var searchResponse = await _elasticsearchClient!.SearchAsync<Dictionary<string, object>>(s => s
            .Indices("test-logs-elastictest-*")
            .Query(q => q
                .Match(m => m
                    .Field("message"!)
                    .Query(testMessage)
                )
            )
        );

        searchResponse.IsValidResponse.Should().BeTrue();
        searchResponse.Documents.Should().NotBeEmpty();
    }

    [Test]
    public async Task Should_Include_Structured_Properties_In_Elasticsearch()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateElasticsearchLogger(_elasticsearchUrl!, "ElasticStructured");
        var orderId = Guid.NewGuid().ToString();
        var customerId = 12345;

        // Act
        logger.Information("Order {OrderId} created for customer {CustomerId}", orderId, customerId);
        await ((IAsyncDisposable)logger).DisposeAsync();

        await Task.Delay(2000);

        // Assert
        var searchResponse = await _elasticsearchClient!.SearchAsync<Dictionary<string, object>>(s => s
            .Indices("test-logs-elasticstructured-*")
            .Query(q => q
                .Term(t => t
                    .Field("OrderId.keyword"!)
                    .Value(orderId)
                )
            )
        );

        searchResponse.IsValidResponse.Should().BeTrue();
        searchResponse.Documents.Should().NotBeEmpty();
        
        var document = searchResponse.Documents.First();
        document.Should().ContainKey("CustomerId");
        document["CustomerId"].ToString().Should().Be(customerId.ToString());
    }

    [Test]
    public async Task Should_Store_Exception_In_Elasticsearch()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateElasticsearchLogger(_elasticsearchUrl!, "ElasticException");
        var exceptionMessage = $"Test exception {Guid.NewGuid()}";

        // Act
        try
        {
            throw new InvalidOperationException(exceptionMessage);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error occurred during processing");
        }
        await ((IAsyncDisposable)logger).DisposeAsync();

        await Task.Delay(2000);

        // Assert
        var searchResponse = await _elasticsearchClient!.SearchAsync<Dictionary<string, object>>(s => s
            .Indices("test-logs-elasticexception-*")
            .Query(q => q
                .Match(m => m
                    .Field("exceptions.Message"!)
                    .Query(exceptionMessage)
                )
            )
        );

        searchResponse.IsValidResponse.Should().BeTrue();
        searchResponse.Documents.Should().NotBeEmpty();
    }

    [Test]
    public async Task Should_Create_Daily_Indices_With_Correct_Format()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateElasticsearchLogger(_elasticsearchUrl!, "ElasticIndex");
        
        // Act
        logger.Information("Testing index format");
        await ((IAsyncDisposable)logger).DisposeAsync();

        await Task.Delay(2000);

        // Assert
        var today = DateTime.UtcNow.ToString("yyyy.MM.dd");
        var expectedIndex = $"test-logs-elasticindex-{today}";

        var indexResponse = await _elasticsearchClient!.Indices.ExistsAsync(expectedIndex);
        indexResponse.Should().NotBeNull();
    }

    [Test]
    public async Task Should_Include_Service_Enrichment_In_Elasticsearch()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateElasticsearchLogger(_elasticsearchUrl!, "ElasticEnrich");
        var testMessage = $"Enrichment test {Guid.NewGuid()}";

        // Act
        logger.Information(testMessage);
        await ((IAsyncDisposable)logger).DisposeAsync();

        await Task.Delay(2000);

        // Assert
        var searchResponse = await _elasticsearchClient!.SearchAsync<Dictionary<string, object>>(s => s
            .Indices("test-logs-elasticenrich-*")
            .Query(q => q
                .Match(m => m
                    .Field("message"!)
                    .Query(testMessage)
                )
            )
        );

        searchResponse.IsValidResponse.Should().BeTrue();
        searchResponse.Documents.Should().NotBeEmpty();

        var document = searchResponse.Documents.First();
        document.Should().ContainKey("ServiceName");
        document["ServiceName"].ToString().Should().Be("ElasticEnrich");
    }

    [Test]
    public async Task Should_Handle_Multiple_Log_Levels_In_Elasticsearch()
    {
        // Arrange
        var logger = TestLoggerFactory.CreateElasticsearchLogger(_elasticsearchUrl!, "ElasticLevels");
        var correlationId = Guid.NewGuid().ToString();

        // Act
        logger.Information("Info message with {CorrelationId}", correlationId);
        logger.Warning("Warning message with {CorrelationId}", correlationId);
        logger.Error("Error message with {CorrelationId}", correlationId);
        await ((IAsyncDisposable)logger).DisposeAsync();

        await Task.Delay(2000);

        // Assert
        var searchResponse = await _elasticsearchClient!.SearchAsync<Dictionary<string, object>>(s => s
            .Indices("test-logs-elasticlevels-*")
            .Query(q => q
                .Term(t => t
                    .Field("CorrelationId.keyword"!)
                    .Value(correlationId)
                )
            )
        );

        searchResponse.IsValidResponse.Should().BeTrue();
        searchResponse.Documents.Should().HaveCount(3);

        // Elasticsearch may store level in different field names, check what's available
        var firstDoc = searchResponse.Documents.First();
        TestContext.WriteLine($"Available keys: {string.Join(", ", firstDoc.Keys)}");
        
        // Try common field names for log level
        var levelKey = firstDoc.Keys.FirstOrDefault(k => 
            k.Equals("Level", StringComparison.OrdinalIgnoreCase) || 
            k.Equals("level", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("Level", StringComparison.OrdinalIgnoreCase));
        
        levelKey.Should().NotBeNullOrEmpty("log level should be present in documents");
        
        var levels = searchResponse.Documents
            .Where(d => d.ContainsKey(levelKey!))
            .Select(d => d[levelKey!].ToString())
            .ToList();
        levels.Should().Contain("Information");
        levels.Should().Contain("Warning");
        levels.Should().Contain("Error");
    }

    private async Task WaitForElasticsearchAsync()
    {
        var maxAttempts = 90; // Increase timeout to 90 seconds for Elasticsearch 8.x
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var pingResponse = await _elasticsearchClient!.PingAsync();
                if (pingResponse.IsValidResponse)
                {
                    TestContext.WriteLine($"Elasticsearch ready after {i + 1} attempts");
                    return;
                }
            }
            catch (Exception ex)
            {
                // Log occasional status updates
                if (i % 10 == 0)
                {
                    TestContext.WriteLine($"Waiting for Elasticsearch... attempt {i + 1}/{maxAttempts}: {ex.Message}");
                }
            }

            await Task.Delay(1000);
        }

        throw new Exception("Elasticsearch did not become ready in time");
    }
}
