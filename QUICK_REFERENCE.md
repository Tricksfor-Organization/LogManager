# LogManager - Quick Reference Guide

## Installation

```bash
# Add to your project
dotnet add reference /path/to/LogManager/src/LogManager/LogManager.csproj

# Or when published to NuGet
dotnet add package LogManager
```

## Minimal Setup (3 Steps)

### 1. Add to `appsettings.json`

```json
{
  "LogManager": {
    "ApplicationName": "YourServiceName",
    "Environment": "Production",
    "MinimumLevel": "Information",
    "Loki": {
      "Enabled": true,
      "Url": "http://loki:3100"
    }
  }
}
```

### 2. Configure in `Program.cs`

```csharp
using LogManager.Extensions;
using LogManager.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add LogManager
Log.Logger = LogManagerExtensions.CreateBootstrapLogger("YourService");
builder.Host.UseLogManager();
builder.Services.AddLogManager(builder.Configuration);

var app = builder.Build();
app.UseCorrelationId();  // Track requests across services

// In .NET 6+, you can use an async Main method:
await app.RunAsync();
await Log.CloseAndFlushAsync();
```

### 3. Use in Your Code

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public async Task DoWork(int id)
    {
        _logger.LogInformation("Processing {Id}", id);
        // Correlation ID automatically included!
    }
}
```

## Configuration Cheat Sheet

### Development Environment
```json
{
  "LogManager": {
    "ApplicationName": "MyService",
    "Environment": "Development",
    "MinimumLevel": "Debug",
    "EnableConsole": true,
    "FileLogging": {
      "Enabled": true,
      "Path": "./logs"
    }
  }
}
```

### Production with Loki (Recommended)
```json
{
  "LogManager": {
    "ApplicationName": "MyService",
    "Environment": "Production",
    "MinimumLevel": "Information",
    "EnableConsole": true,
    "FileLogging": {
      "Enabled": true,
      "Path": "/var/log/app",
      "RetainedFileCountLimit": 31
    },
    "Loki": {
      "Enabled": true,
      "Url": "http://loki:3100",
      "Labels": {
        "service": "myservice",
        "environment": "production"
      }
    }
  }
}
```

### Production with ELK
```json
{
  "LogManager": {
    "ApplicationName": "MyService",
    "Environment": "Production",
    "MinimumLevel": "Information",
    "Elasticsearch": {
      "Enabled": true,
      "NodeUris": ["http://elasticsearch:9200"],
      "IndexFormat": "logs-myservice-{{0:yyyy.MM.dd}}",
      "Username": "${ELASTICSEARCH_USERNAME}",
      "Password": "${ELASTICSEARCH_PASSWORD}"
    }
  }
}
```

## Docker Compose Integration

```yaml
services:
  myservice:
    image: myservice:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - SERVICE_VERSION=1.0.0
      - POD_NAME=myservice-pod
      - ELASTICSEARCH_USERNAME=elastic
      - ELASTICSEARCH_PASSWORD=changeme
    volumes:
      - ./logs:/var/log/app
    depends_on:
      - loki
      - elasticsearch

  loki:
    image: grafana/loki:2.9.3
    ports:
      - "3100:3100"

  grafana:
    image: grafana/grafana:10.2.2
    ports:
      - "3000:3000"
```

## Kubernetes Integration

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myservice
spec:
  template:
    spec:
      containers:
      - name: myservice
        env:
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: POD_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
```

## Common Log Patterns

### Structured Logging
```csharp
_logger.LogInformation("Order {OrderId} created for {CustomerId}", orderId, customerId);
_logger.LogInformation("Processing {@Order}", order); // Logs entire object
```

### Error Handling
```csharp
try
{
    // Your code
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process {OrderId}", orderId);
    throw;
}
```

### Using Scopes
```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["UserId"] = userId,
    ["Operation"] = "Checkout"
}))
{
    _logger.LogInformation("Starting checkout");
    // All logs in this scope include UserId and Operation
}
```

### Manual Correlation ID
```csharp
// For background jobs or message consumers
DefaultCorrelationIdAccessor.SetCorrelationId(message.CorrelationId);
```

## gRPC Setup

```csharp
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<CorrelationIdInterceptor>();
});
```

## Log Levels

| Level | When to Use |
|-------|-------------|
| `Debug` | Detailed diagnostic information |
| `Information` | General application flow |
| `Warning` | Abnormal but expected events |
| `Error` | Errors and exceptions |
| `Fatal` | Critical failures requiring immediate attention |

## Environment Variables

```bash
# Service identification
SERVICE_VERSION=1.0.0
SERVICE_ID=service-001

# Kubernetes (auto-injected)
POD_NAME=myservice-abc123
POD_NAMESPACE=production
HOSTNAME=container-id

# Secrets (use HashiCorp Vault in production)
ELASTICSEARCH_USERNAME=elastic
ELASTICSEARCH_PASSWORD=secret
LOKI_USERNAME=user
LOKI_PASSWORD=secret
```

## Viewing Logs

### Grafana (with Loki)
```logql
# All logs for your service
{app="myservice"}

# Errors only
{app="myservice"} |= "error"

# Specific correlation ID
{app="myservice"} | json | CorrelationId="abc-123"

# Last hour
{app="myservice"} [1h]
```

### Kibana (with Elasticsearch)
```
# All logs for your service
ServiceName: "myservice"

# Errors
Level: "Error"

# Correlation ID
CorrelationId: "abc-123"
```

## Troubleshooting

### No logs appearing
1. Check console output first
2. Verify Loki/Elasticsearch URL is correct
3. Check network connectivity: `curl http://loki:3100/ready`
4. Enable Serilog self-diagnostics:
   ```csharp
   Serilog.Debugging.SelfLog.Enable(Console.Error);
   ```

### High memory usage
```json
{
  "Loki": {
    "BatchPostingLimit": 500,
    "QueueLimit": 50000,
    "Period": "00:00:01"
  }
}
```

## Performance Tips

1. Use structured logging (faster than string interpolation)
   ```csharp
   // Good
   _logger.LogInformation("User {UserId} logged in", userId);
   
   // Avoid
   _logger.LogInformation($"User {userId} logged in");
   ```

2. Don't log in tight loops
3. Use appropriate log levels
4. Consider sampling for high-frequency events

## Best Practices

✅ **DO:**
- Use correlation IDs for request tracking
- Log at service boundaries
- Include structured data
- Use log levels appropriately
- Handle secrets securely

❌ **DON'T:**
- Log sensitive data (passwords, tokens, PII)
- Log in performance-critical paths
- Use string interpolation for log messages
- Ignore log volume in production

## Additional Resources

- Full Examples: `/examples/` directory
- Docker Compose: `/examples/docker-compose.yml`
- Kubernetes: `/examples/kubernetes-deployment.yaml`
- Complete Documentation: `/examples/README.md`
