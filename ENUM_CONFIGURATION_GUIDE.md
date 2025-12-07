# Enum-Based Configuration Feature

This document describes the new enum-based configuration feature added to LogManager, which provides type-safe configuration options and optional access to the service collection during setup.

## Overview

This feature adds:
1. **Type-safe enums** for `MinimumLevel` and `RollingInterval` 
2. **New overload** of `AddLogManager` that provides access to `IServiceCollection`
3. **Full backward compatibility** - existing string-based configuration continues to work

## New Enums

### LogLevel Enum

```csharp
public enum LogLevel
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}
```

### RollingInterval Enum

```csharp
public enum RollingInterval
{
    Infinite,  // Never roll
    Year,      // Roll every year
    Month,     // Roll every month
    Day,       // Roll every day
    Hour,      // Roll every hour
    Minute     // Roll every minute
}
```

## Usage

### 1. Simple Enum-Based Configuration

```csharp
services.AddLogManager(opts =>
{
    opts.ApplicationName = "MyApp";
    opts.MinimumLevelEnum = LogLevel.Warning;  // Type-safe!
    
    opts.FileLogging = new FileLoggingOptions
    {
        Enabled = true,
        Path = "/var/log/myapp",
        RollingIntervalEnum = RollingInterval.Hour  // No string magic!
    };
});
```

### 2. Configuration with Service Collection Access

The new overload allows you to access other registered services during configuration:

```csharp
// Register some configuration
services.Configure<MyAppOptions>(configuration.GetSection("MyApp"));

// Use it in LogManager setup
services.AddLogManager((opts, serviceCollection) =>
{
    // Access other services/options
    using var tempProvider = serviceCollection!.BuildServiceProvider();
    var myAppOptions = tempProvider.GetService<IOptions<MyAppOptions>>()?.Value;

    if (myAppOptions != null)
    {
        opts.ApplicationName = myAppOptions.AppName;
        opts.MinimumLevelEnum = ParseLogLevel(myAppOptions.LogLevel);
        opts.FileLogging = new FileLoggingOptions
        {
            Path = myAppOptions.LogPath,
            RollingIntervalEnum = RollingInterval.Day
        };
    }
});
```

### 3. Backward Compatibility

All existing code continues to work without changes:

```csharp
// String-based configuration still works
services.AddLogManager(opts =>
{
    opts.MinimumLevel = "Information";  // ✅ Still works!
    opts.FileLogging = new FileLoggingOptions
    {
        RollingInterval = "Day"  // ✅ Still works!
    };
});

// Configuration from appsettings.json unchanged
services.AddLogManager(configuration, "LogManager");  // ✅ Still works!
```

## API Reference

### New Properties

#### LogManagerOptions

```csharp
/// <summary>
/// Minimum log level as enum (preferred for code-based configuration)
/// </summary>
public LogLevel? MinimumLevelEnum { get; set; }
```

#### FileLoggingOptions

```csharp
/// <summary>
/// Rolling interval as enum (preferred for code-based configuration)
/// </summary>
public RollingInterval? RollingIntervalEnum { get; set; }
```

### New Extension Method

```csharp
/// <summary>
/// Add LogManager with custom options and optional access to service collection
/// </summary>
public static IServiceCollection AddLogManager(
    this IServiceCollection services,
    Action<LogManagerOptions, IServiceCollection?> configureOptions)
```

## Priority Rules

When both enum and string properties are set:
- **Enum takes precedence** over string
- If enum is `null`, the string value is used
- If both are `null`, defaults apply

```csharp
opts.MinimumLevel = "Debug";           // Will be ignored
opts.MinimumLevelEnum = LogLevel.Error; // This wins!
```

## Benefits

### 1. IntelliSense Support
No more guessing valid values - your IDE shows all options:

```csharp
opts.MinimumLevelEnum = LogLevel.   // IDE shows: Verbose, Debug, Information, ...
```

### 2. Compile-Time Safety
Typos are caught at compile time:

```csharp
opts.MinimumLevel = "Informaton";      // ❌ Runtime error (typo)
opts.MinimumLevelEnum = LogLevel.Info; // ✅ Compile error (caught immediately)
```

### 3. Refactoring Support
Renaming enum values updates all usages automatically - safer than strings.

### 4. Service Collection Access
Configure LogManager based on other registered services:

```csharp
services.AddLogManager((opts, services) =>
{
    // Fetch feature flags, environment config, etc.
    var featureFlags = services!.BuildServiceProvider()
        .GetService<IFeatureFlags>();
    
    if (featureFlags?.EnableVerboseLogging == true)
    {
        opts.MinimumLevelEnum = LogLevel.Debug;
    }
});
```

## Migration Guide

### Migrating Existing Code

No migration required! Your existing code works as-is. When you're ready to adopt enums:

**Before:**
```csharp
services.AddLogManager(opts =>
{
    opts.MinimumLevel = "Warning";
    opts.FileLogging = new FileLoggingOptions
    {
        RollingInterval = "Hour"
    };
});
```

**After:**
```csharp
services.AddLogManager(opts =>
{
    opts.MinimumLevelEnum = LogLevel.Warning;
    opts.FileLogging = new FileLoggingOptions
    {
        RollingIntervalEnum = RollingInterval.Hour
    };
});
```

### appsettings.json

No changes needed - JSON configuration uses strings as before:

```json
{
  "LogManager": {
    "MinimumLevel": "Information",
    "FileLogging": {
      "RollingInterval": "Day"
    }
  }
}
```

## Implementation Details

The enum properties are nullable (`LogLevel?`, `RollingInterval?`), allowing the system to detect when they're explicitly set versus using defaults. Internal converter methods map between the enum types and Serilog's types.

## Examples

See `src/LogManager/Examples/EnumConfigurationExamples.cs` for complete working examples.

## Testing

All existing tests pass without modification, proving backward compatibility. The feature has been tested with:
- ✅ Enum-based configuration
- ✅ String-based configuration  
- ✅ Mixed enum/string configuration
- ✅ Service collection access scenarios
- ✅ appsettings.json binding

## Questions?

See the main README.md or check the XML documentation on the public APIs.
