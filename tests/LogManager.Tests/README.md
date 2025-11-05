# LogManager Test Suite Documentation

## Overview
Comprehensive integration tests for the LogManager library using NUnit, FluentAssertions, NSubstitute, and TestContainers.

## Test Structure

### File Logging Tests (`FileLoggingTests.cs`)
Fast unit/integration tests that verify file-based logging functionality.

**Tests:**
1. ✅ `Should_Create_Log_File_When_Logging` - Verifies log files are created and contain expected messages
2. ✅ `Should_Include_Structured_Properties_In_Log` - Tests structured logging with properties
3. ✅ `Should_Log_Exceptions_With_Stack_Trace` - Validates exception logging with full stack traces
4. ✅ `Should_Include_Service_Context_Enrichment` - Ensures enrichment is applied
5. ✅ `Should_Create_Multiple_Log_Files_Within_Size_Limit` - Tests file rolling based on size
6. ✅ `Should_Handle_Relative_Path` - Verifies relative path handling
7. ✅ `Should_Support_Different_Log_Levels` - Tests all log levels (Verbose to Fatal)

**Status:** ✅ All 7 tests passing

### Elasticsearch Logging Tests (`ElasticsearchLoggingTests.cs`)
Integration tests using Testcontainers.Elasticsearch to spin up a real Elasticsearch instance.

**Tests:**
1. `Should_Send_Logs_To_Elasticsearch` - Verifies logs are sent and indexed
2. `Should_Include_Structured_Properties_In_Elasticsearch` - Tests structured data in ES
3. `Should_Store_Exception_Details_In_Elasticsearch` - Validates exception storage
4. `Should_Create_Daily_Indices_With_Correct_Format` - Tests index naming pattern
5. `Should_Include_Service_Enrichment_In_Elasticsearch` - Verifies enrichment in ES
6. `Should_Handle_Multiple_Log_Levels_In_Elasticsearch` - Tests log level filtering

**Container:** `docker.elastic.co/elasticsearch/elasticsearch:8.11.0`
**Status:** ⏳ Ready to run (requires Docker)

### Loki Logging Tests (`LokiLoggingTests.cs`)
Integration tests using Testcontainers to spin up a Grafana Loki instance.

**Tests:**
1. `Should_Send_Logs_To_Loki` - Verifies logs are sent to Loki
2. `Should_Include_Structured_Properties_In_Loki` - Tests structured logging
3. `Should_Store_Exception_In_Loki` - Validates exception logging
4. `Should_Apply_Custom_Labels_In_Loki` - Tests label-based filtering
5. `Should_Handle_Different_Log_Levels_In_Loki` - Tests log levels
6. `Should_Support_High_Volume_Logging_To_Loki` - Performance test with 100 logs
7. `Should_Include_Service_Context_In_Loki` - Verifies enrichment

**Container:** `grafana/loki:2.9.3`
**Status:** ⏳ Ready to run (requires Docker)

## Test Infrastructure

### Test Helpers (`Infrastructure/TestHelpers.cs`)
- `CreateTempDirectory()` - Creates isolated test directories
- `CleanupDirectory()` - Removes test artifacts
- `WaitForConditionAsync()` - Polling utility for async conditions
- `ReadFileWithRetryAsync()` - Handles file locking during reads

### Test Logger Factory (`Infrastructure/TestLoggerFactory.cs`)
- `CreateFileLogger()` - Configures file sink for testing
- `CreateElasticsearchLogger()` - Configures ES sink with immediate batching
- `CreateLokiLogger()` - Configures Loki sink with immediate batching

## Running the Tests

### All Tests
```bash
dotnet test
```

### File Tests Only (Fast)
```bash
dotnet test --filter "FullyQualifiedName~FileLoggingTests"
```

### Elasticsearch Tests
```bash
# Requires Docker running
dotnet test --filter "FullyQualifiedName~ElasticsearchLoggingTests"
```

### Loki Tests
```bash
# Requires Docker running
dotnet test --filter "FullyQualifiedName~LokiLoggingTests"
```

### With Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Categories

| Category | Test Count | Duration | Docker Required |
|----------|-----------|----------|-----------------|
| File Logging | 7 | ~5s | No |
| Elasticsearch | 6 | ~30s | Yes |
| Loki | 7 | ~30s | Yes |
| **Total** | **20** | **~65s** | **Partial** |

## Key Features Tested

### Functional
- ✅ Log file creation and rotation
- ✅ Daily rolling intervals
- ✅ Size-based file rolling
- ✅ Multiple log levels
- ✅ Structured logging
- ✅ Exception logging with stack traces
- ✅ Relative and absolute paths

### Integration
- ⏳ Elasticsearch indexing
- ⏳ Loki label-based queries
- ⏳ Daily index creation (ES)
- ⏳ High-volume logging
- ⏳ Service enrichment persistence

### Configuration
- ✅ Configurable output templates
- ✅ Batching configuration
- ✅ Retention policies
- ✅ Custom enrichment

## CI/CD Considerations

### GitHub Actions Example
```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      elasticsearch:
        image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
        env:
          discovery.type: single-node
          xpack.security.enabled: false
        ports:
          - 9200:9200
      
      loki:
        image: grafana/loki:2.9.3
        ports:
          - 3100:3100

    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Restore
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore
      
      - name: Test (File)
        run: dotnet test --filter "FullyQualifiedName~FileLoggingTests" --no-build
      
      - name: Test (Elasticsearch)
        run: dotnet test --filter "FullyQualifiedName~ElasticsearchLoggingTests" --no-build
      
      - name: Test (Loki)
        run: dotnet test --filter "FullyQualifiedName~LokiLoggingTests" --no-build
```

## Best Practices Demonstrated

1. **Isolation** - Each test uses its own directory/index/labels
2. **Cleanup** - Proper teardown with TearDown/OneTimeTearDown
3. **Async** - All tests are properly async/await
4. **Wait Strategies** - TestContainers wait for services to be ready
5. **Retry Logic** - File operations retry on locks
6. **Correlation** - Tests use unique IDs for filtering
7. **Assertions** - Fluent assertions for readable test failures

## Troubleshooting

### Tests Timeout
- Increase wait times in container setup (default: 30s)
- Check Docker daemon is running
- Verify network connectivity

### File Lock Errors
- Tests use `Shared = true` for file sinks
- Retry logic handles transient locks
- TearDown cleanup may need delay

### Container Start Failures
- Check Docker resources (memory/CPU)
- Review container logs
- Verify image versions are available

### Elasticsearch Connection Refused
- Wait strategy may need adjustment
- Check ES container logs
- Verify port mapping

## Future Enhancements

- [ ] Add performance benchmarks
- [ ] Test correlation ID propagation
- [ ] Test middleware integration
- [ ] Test gRPC interceptor
- [ ] Add stress tests
- [ ] Test HashiCorp Vault integration
- [ ] Test Kubernetes configuration
- [ ] Add mutation testing
