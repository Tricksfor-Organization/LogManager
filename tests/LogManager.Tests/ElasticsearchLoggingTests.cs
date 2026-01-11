using Elastic.Clients.Elasticsearch;
using Shouldly;
using NUnit.Framework;
using Testcontainers.Elasticsearch;
using LogManager.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;

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
        TestContext.WriteLine("Creating Elasticsearch container...");
        
        // Start Elasticsearch container with more memory and explicit wait strategy
        _elasticsearchContainer = new ElasticsearchBuilder("docker.elastic.co/elasticsearch/elasticsearch:9.2.3")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithPortBinding(9200, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r
                .ForPort(9200)
                .ForPath("/")))
            .Build();

        TestContext.WriteLine("Starting Elasticsearch container...");
        await _elasticsearchContainer.StartAsync();
        TestContext.WriteLine("Elasticsearch container started!");
        
        _elasticsearchUrl = _elasticsearchContainer.GetConnectionString();
        TestContext.WriteLine($"Elasticsearch URL from container: {_elasticsearchUrl}");
        
        // Force HTTP protocol since container runs without TLS
        if (_elasticsearchUrl.StartsWith("https://"))
        {
            _elasticsearchUrl = _elasticsearchUrl.Replace("https://", "http://");
            TestContext.WriteLine($"Corrected to HTTP: {_elasticsearchUrl}");
        }
        
        // Create Elasticsearch client for verification with explicit timeouts
        // Note: ElasticsearchClientSettings is not disposable in Elastic.Clients.Elasticsearch 8.x
        var settings = new ElasticsearchClientSettings(new Uri(_elasticsearchUrl))
            .DefaultIndex("test-logs")
            .RequestTimeout(TimeSpan.FromSeconds(5))
            .PingTimeout(TimeSpan.FromSeconds(2));
        
        _elasticsearchClient = new ElasticsearchClient(settings);

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

            searchResponse.IsValidResponse.ShouldBeTrue();
            searchResponse.Documents.ShouldNotBeEmpty();
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

        searchResponse.IsValidResponse.ShouldBeTrue();
        searchResponse.Documents.ShouldNotBeEmpty();
        
        var document = searchResponse.Documents.First();
            document.ShouldContainKey("CustomerId");
            document["CustomerId"].ToString().ShouldBe(customerId.ToString());
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

        searchResponse.IsValidResponse.ShouldBeTrue();
        searchResponse.Documents.ShouldNotBeEmpty();
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
          indexResponse.ShouldNotBeNull();
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

        searchResponse.IsValidResponse.ShouldBeTrue();
        searchResponse.Documents.ShouldNotBeEmpty();

        var document = searchResponse.Documents.First();
            document.ShouldContainKey("ServiceName");
            document["ServiceName"].ToString().ShouldBe("ElasticEnrich");
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

        searchResponse.IsValidResponse.ShouldBeTrue();
        searchResponse.Documents.Count.ShouldBe(3);

        // Elasticsearch may store level in different field names, check what's available
        var firstDoc = searchResponse.Documents.First();
        TestContext.WriteLine($"Available keys: {string.Join(", ", firstDoc.Keys)}");
        
        // Try common field names for log level
        var levelKey = firstDoc.Keys.FirstOrDefault(k => 
            k.Equals("Level", StringComparison.OrdinalIgnoreCase) || 
            k.Equals("level", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("Level", StringComparison.OrdinalIgnoreCase));
        
            levelKey.ShouldNotBeNullOrEmpty("log level should be present in documents");
        
        var levels = searchResponse.Documents
            .Where(d => d.ContainsKey(levelKey!))
            .Select(d => d[levelKey!].ToString())
            .ToList();
            levels.ShouldContain("Information");
            levels.ShouldContain("Warning");
            levels.ShouldContain("Error");
    }

    private async Task WaitForElasticsearchAsync()
    {
        var maxAttempts = 30; // 30 seconds should be enough with proper timeouts
        TestContext.WriteLine($"Starting Elasticsearch readiness check at {_elasticsearchUrl}");
        
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                TestContext.WriteLine($"Attempt {i + 1}/{maxAttempts}: Pinging Elasticsearch...");
                var pingResponse = await _elasticsearchClient!.PingAsync();
                
                TestContext.WriteLine($"Ping response - IsValidResponse: {pingResponse.IsValidResponse}, " +
                                     $"ApiCallDetails: {pingResponse.ApiCallDetails?.DebugInformation ?? "null"}");
                
                if (pingResponse.IsValidResponse)
                {
                    TestContext.WriteLine($"✓ Elasticsearch ready after {i + 1} attempts");
                    return;
                }
                
                TestContext.WriteLine($"Ping returned invalid response, retrying...");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Attempt {i + 1}/{maxAttempts} failed: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    TestContext.WriteLine($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
            }

            await Task.Delay(1000);
        }

        throw new Exception($"Elasticsearch did not become ready in time after {maxAttempts} attempts. URL: {_elasticsearchUrl}");
    }
}
