# LogManager

A focused Serilog-based logging library for .NET apps and microservices. It provides a single, consistent configuration that can write logs to:

- Console (for local/dev)
- Files with daily rolling and retention
- Elasticsearch (ELK)
- Grafana Loki

It also includes production-friendly enrichers (service metadata, container info, correlation IDs), HTTP middleware, and a gRPC interceptor to propagate correlation IDs end-to-end.


## Features at a glance

- Multi-sink: Console, File, Elasticsearch, Loki
- Simple DI integration with extension methods
- HTTP correlation ID middleware and gRPC interceptor
- Rich enrichment: machine/environment/thread/process/exception details, container metadata, service name/version
- App- and environment-aware defaults; Docker-friendly file logging
- Works in Web, Worker, gRPC, and console apps


## Install / Reference

This repository contains a class library project (`src/LogManager`). You can:

- Reference it directly from your solution as a ProjectReference, or
- Package and publish it to your private feed/NuGet, then add the package to your apps

Once referenced, import the extension namespace where you configure DI:

```csharp
using LogManager.Extensions;     // AddLogManager, UseLogManager, CreateBootstrapLogger
using LogManager.Middleware;     // UseCorrelationId()
```


## Quick start (ASP.NET Core minimal API)

Program.cs:

```csharp
using LogManager.Extensions;
using LogManager.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1) Bind options from configuration ("LogManager" section by default)
builder.Services.AddLogManager(builder.Configuration);

// Optional: override file path via DI (ensures file logging is enabled)
builder.Services.AddLogManagerFilePath("/var/log/orders-api");

// 2) Wire Serilog via LogManager
builder.Host.UseLogManager();

var app = builder.Build();

// 3) Add correlation ID middleware (adds/propagates X-Correlation-ID)
app.UseCorrelationId();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.LogInformation("Hello from {App} at {Time}", "Orders.Api", DateTimeOffset.UtcNow);
    return Results.Ok(new { ok = true });
});

app.Run();
```


## Configuration

LogManager binds from a `LogManager` section in configuration (appsettings.json, environment, etc.). All options have sensible defaults and can be overridden.

appsettings.json example:

```json
{
	"LogManager": {
		"ApplicationName": "Orders.Api",
		"Environment": "Development",
		"MinimumLevel": "Information",
		"EnableConsole": true,

		"Enrichment": {
			"WithMachineName": true,
			"WithEnvironmentName": true,
			"WithThreadId": true,
			"WithProcessId": true,
			"WithProcessName": true,
			"WithExceptionDetails": true,
			"CustomProperties": {
				"team": "core",
				"service": "orders"
			}
		},

		"FileLogging": {
			"Enabled": true,
			"Path": "/var/log/orders",
			"FileNamePattern": "orders-.log",
			"RollingInterval": "Day",
			"RetainedFileCountLimit": 14,
			"FileSizeLimitBytes": 10485760,
			"RollOnFileSizeLimit": true,
			"Shared": true,
			"Buffered": true,
			"OutputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"
		},

		"Elasticsearch": {
			"Enabled": false,
			"NodeUris": [ "http://localhost:9200" ],
			"IndexFormat": "logs-orders-{{0:yyyy.MM.dd}}",
			"BatchPostingLimit": 50,
			"Period": "00:00:02",
			"AutoRegisterTemplate": true,
			"NumberOfShards": 1,
			"NumberOfReplicas": 1,
			"EmitEventFailure": true,
			"ConnectionTimeout": "00:00:05"
		},

		"Loki": {
			"Enabled": false,
			"Url": "http://localhost:3100",
			"Labels": {
				"service": "orders",
				"region": "eu"
			},
			"BatchPostingLimit": 1000,
			"Period": "00:00:02",
			"QueueLimit": 100000,
			"OutputTemplate": "[{Level:u3}] {Message:lj}{NewLine}{Exception}"
		},

		"MinimumLevelOverrides": {
			"Microsoft": "Warning",
			"Microsoft.Hosting.Lifetime": "Information",
			"System": "Warning"
		}
	}
}
```

Notes:
- File `Path` can be relative or absolute. Relative paths are expanded under the app base directory, and the directory is created if missing.
- Elasticsearch `IndexFormat` is used by the sink to append the date. Because LogManager also injects your `ApplicationName`, use double braces around the date token so it’s preserved for the sink, for example: `logs-orders-{{0:yyyy.MM.dd}}`.
- For secrets like Elasticsearch or Loki credentials, prefer environment variables or a secret manager (e.g., HashiCorp Vault) rather than committing them to config.


## Programmatic configuration

You can also configure LogManager purely in code:

```csharp
builder.Services.AddLogManager(options =>
{
    options.ApplicationName = "Orders.Api";
    options.Environment = builder.Environment.EnvironmentName;
    options.MinimumLevel = "Information";
    options.EnableConsole = true;

    options.FileLogging = new()
    {
        Enabled = true,
        Path = "/var/log/orders",
        FileNamePattern = "orders-.log",
        RollingInterval = "Day"
    };

    options.Loki = new()
    {
        Enabled = true,
        Url = "http://loki:3100",
        Labels = new() { ["service"] = "orders" }
    };
});

builder.Host.UseLogManager(options =>
{
    // You can also attach sinks/enrichers by setting options here
    options.MinimumLevel = "Debug";
});
```

### Type-safe enum configuration

For code-based configuration, you can use type-safe enums instead of strings for better IDE support and compile-time safety:

```csharp
using LogManager.Configuration;

builder.Services.AddLogManager(options =>
{
    options.ApplicationName = "Orders.Api";
    
    // Use enums for type safety and IntelliSense support
    options.MinimumLevelEnum = LogLevel.Warning;  // Instead of "Warning"
    
    options.FileLogging = new()
    {
        Enabled = true,
        Path = "/var/log/orders",
        RollingIntervalEnum = FileRollingInterval.Hour  // Instead of "Hour"
    };
});
```

**Available LogLevel values (Microsoft.Extensions.Logging.LogLevel):** `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`

**Note:** `Trace` maps to Serilog's `Verbose`, and `Critical` maps to Serilog's `Fatal`.

**Available RollingInterval values:** `Infinite`, `Year`, `Month`, `Day`, `Hour`, `Minute`

**Note:** Enum properties take precedence over string properties. String-based configuration continues to work for backward compatibility and when binding from JSON configuration.

### Configuration with service provider access

If you need to access other registered services or options during LogManager configuration, use the overload that provides `IServiceProvider`. This uses the proper `IConfigureOptions` pattern for safe service resolution:

```csharp
// First, register your app-specific options
builder.Services.Configure<MyAppOptions>(builder.Configuration.GetSection("MyApp"));

// Then configure LogManager with access to the service provider
builder.Services.AddLogManager((options, serviceProvider) =>
{
    options.ApplicationName = "Orders.Api";
    
    // Safely resolve other services/options (no BuildServiceProvider needed!)
    var myAppOptions = serviceProvider.GetService<IOptions<MyAppOptions>>()?.Value;
    
    if (myAppOptions != null)
    {
        // Configure LogManager based on other options
        options.MinimumLevelEnum = myAppOptions.EnableDebugLogging 
            ? Microsoft.Extensions.Logging.LogLevel.Debug 
            : Microsoft.Extensions.Logging.LogLevel.Information;
        
        options.FileLogging = new()
        {
            Enabled = true,
            Path = myAppOptions.LogPath,
            RollingIntervalEnum = FileRollingInterval.Day
        };
    }
});
```

This is useful when LogManager needs configuration values from feature flags, database settings, or other parts of your application.

**Note:** This overload uses `IServiceProvider` (not `IServiceCollection`) through the `IConfigureOptions` pattern. The framework handles service resolution at the proper time with correct lifetimes - you never need to call `BuildServiceProvider()` yourself.

**Alternative approach** using the options pattern directly:

```csharp
// Register LogManager first
builder.Services.AddLogManager(builder.Configuration);

// Then configure it based on other options
builder.Services.AddOptions<LogManagerOptions>()
    .Configure<IOptions<MyAppOptions>>((logOpts, myAppOpts) =>
    {
        if (myAppOpts.Value.EnableDebugLogging)
        {
            logOpts.MinimumLevelEnum = Microsoft.Extensions.Logging.LogLevel.Debug;
        }
        logOpts.FileLogging = new()
        {
            Enabled = true,
            Path = myAppOpts.Value.LogPath,
            RollingIntervalEnum = FileRollingInterval.Day
        };
    });
```

Both approaches are valid - use whichever fits your scenario better.


## Correlation IDs

- HTTP: Add `app.UseCorrelationId();` to automatically read or create an `X-Correlation-ID` (or fallback to `X-Request-ID`), store it in AsyncLocal, and include it in logs and response headers.
- gRPC: Register interceptor to propagate the correlation ID via metadata key `x-correlation-id`:

```csharp
using LogManager.Interceptors;

builder.Services.AddGrpc(options =>
{
		options.Interceptors.Add<CorrelationIdInterceptor>();
});
```

The correlation ID is available in logs via the `CorrelationId` property and included by default in the file output template.


## Enrichment

Enabled by default; can be toggled in `Enrichment` options:

- Machine name
- Environment name (from options.Environment)
- Thread ID, Process ID, Process name
- Exception details (Serilog.Exceptions)
- Container metadata (Docker/Kubernetes-friendly)
- Service context (ApplicationName, Version from `SERVICE_VERSION` or entry assembly, optional `SERVICE_ID` from env)
- Custom properties via `Enrichment.CustomProperties`


## Sinks

### Console
Enabled by `EnableConsole = true`. Uses a compact template with service name.

### File
- Daily rolling by default (`RollingInterval = Day`)
- Retention via `RetainedFileCountLimit`
- Size-based rolling via `FileSizeLimitBytes` + `RollOnFileSizeLimit`
- Docker-friendly (`Shared = true`)

### Elasticsearch
- Configure `NodeUris` and `IndexFormat`
- Use `"logs-orders-{{0:yyyy.MM.dd}}"` to include daily indices
- Supports `Username`/`Password` or `ApiKey`
- Batching and template auto-registration controls included

### Loki
- Configure `Url` and static `Labels` (e.g., app/environment/region)
- Optional basic auth (`Username`/`Password`) and multi-tenant `TenantId`
- Tunable queue and batching


## Bootstrap logger (early startup logging)

To capture logs before the host is built:

```csharp
using LogManager.Extensions;

Log.Logger = LogManager.Extensions.LogManagerExtensions.CreateBootstrapLogger(
		applicationName: "Orders.Api",
		environment: Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development");

try
{
		Log.Information("Starting up");
		// build and run host...
}
catch (Exception ex)
{
		Log.Fatal(ex, "Application start-up failed");
}
finally
{
		await Log.CloseAndFlushAsync();
}
```


## Usage tips

- Prefer structured logging: `logger.LogInformation("Processed order {OrderId} for {Customer}", orderId, customer);`
- In containers, mount a host volume to the `FileLogging.Path` to persist logs
- For production, consider disabling console logs to reduce noise and rely on sinks (File/Elasticsearch/Loki)
- Tune `BatchPostingLimit`, `Period`, and `QueueLimit` for your throughput


## Testing and examples

See `tests/LogManager.Tests` for examples covering File, Elasticsearch (via Testcontainers), and Loki (via Testcontainers). These tests demonstrate configuration patterns and validate that logs are emitted and queryable.

For detailed examples of the new enum-based configuration and service collection access features, see:
- `src/LogManager/Examples/EnumConfigurationExamples.cs` - Complete working examples
- `ENUM_CONFIGURATION_GUIDE.md` - Comprehensive guide with migration tips


## License

See `LICENSE` for details.

