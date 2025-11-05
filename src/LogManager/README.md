# LogManager

A lightweight, production-ready Serilog configuration library for .NET apps and microservices.

- Sinks: Console, File (daily rolling), Elasticsearch (ELK), Grafana Loki
- Enrichment: service name/version, machine/env, thread/process, exception details, container metadata, correlation ID
- Middleware and gRPC interceptor for correlation ID propagation

## Install

Add the package to your project and import the extensions:

```csharp
using LogManager.Extensions;   // AddLogManager, UseLogManager, CreateBootstrapLogger
using LogManager.Middleware;   // UseCorrelationId()
```

## Quick start (ASP.NET Core)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Bind from configuration section "LogManager"
builder.Services.AddLogManager(builder.Configuration);

// Optional: override file path via DI
builder.Services.AddLogManagerFilePath("/var/log/myapp");

// Plug Serilog via LogManager
builder.Host.UseLogManager();

var app = builder.Build();

// Correlation ID middleware (X-Correlation-ID)
app.UseCorrelationId();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.LogInformation("Hello from {App}", "MyApp");
    return Results.Ok();
});

app.Run();
```

## Minimal configuration (appsettings.json)

```json
{
  "LogManager": {
    "ApplicationName": "MyApp",
    "Environment": "Production",
    "MinimumLevel": "Information",
    "EnableConsole": true,
    "FileLogging": {
      "Enabled": true,
      "Path": "/var/log/myapp",
      "FileNamePattern": "app-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 14
    },
    "Loki": { "Enabled": false, "Url": "http://loki:3100" },
    "Elasticsearch": { "Enabled": false, "NodeUris": ["http://localhost:9200"],
      "IndexFormat": "logs-myapp-{{0:yyyy.MM.dd}}" }
  }
}
```

Notes:
- Relative file paths are expanded under the app base directory and created if missing.
- For Elasticsearch `IndexFormat`, keep the date token in double braces so the sink formats it daily.

## More

- Source & full docs: https://github.com/Tricksfor-Organization/LogManager
- License: MIT
