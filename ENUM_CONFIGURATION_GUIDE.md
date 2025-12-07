# Enum-Based Configuration Feature

This document describes the new enum-based configuration feature added to LogManager, which provides type-safe configuration options and optional access to the service collection during setup.

## Overview

This feature adds:
1. **Type-safe enums** for `MinimumLevel` (using standard `Microsoft.Extensions.Logging.LogLevel`) and `FileRollingInterval` 
2. **New overload** of `AddLogManager` that provides access to `IServiceProvider` using the proper `IConfigureOptions` pattern
3. **Full backward compatibility** - existing string-based configuration continues to work

## New Enums

### LogLevel Enum

LogManager uses the standard `Microsoft.Extensions.Logging.LogLevel` enum that .NET developers are already familiar with:

```csharp
public enum LogLevel
{
    Trace = 0,      // Maps to Serilog Verbose
    Debug = 1,      // Maps to Serilog Debug
    Information = 2,// Maps to Serilog Information
    Warning = 3,    // Maps to Serilog Warning
    Error = 4,      // Maps to Serilog Error
    Critical = 5,   // Maps to Serilog Fatal
    None = 6        // Maps to Serilog Fatal
}
```

### RollingInterval Enum

```csharp
public enum FileRollingInterval
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
using Microsoft.Extensions.Logging;  // For LogLevel
using LogManager.Configuration;      // For FileRollingInterval

services.AddLogManager(opts =>
{
    opts.ApplicationName = "MyApp";
    opts.MinimumLevelEnum = LogLevel.Warning;  // Type-safe!
    
    opts.FileLogging = new FileLoggingOptions
    {
        Enabled = true,
        Path = "/var/log/myapp",
        RollingIntervalEnum = FileRollingInterval.Hour  // No string magic!
    };
});
```

### 2. Configuration with Service Provider Access

The new overload allows you to access other registered services during configuration using proper `IServiceProvider` pattern:

```csharp
// Register some configuration
services.Configure<MyAppOptions>(configuration.GetSection("MyApp"));

// Use it in LogManager setup with IServiceProvider
services.AddLogManager((opts, serviceProvider) =>
{
    // Safely resolve other services/options
    var myAppOptions = serviceProvider.GetService<IOptions<MyAppOptions>>()?.Value;

    if (myAppOptions != null)
    {
        opts.ApplicationName = myAppOptions.AppName;
        opts.MinimumLevelEnum = ParseLogLevel(myAppOptions.LogLevel);
        opts.FileLogging = new FileLoggingOptions
        {
            Path = myAppOptions.LogPath,
            RollingIntervalEnum = FileRollingInterval.Day
        };
    }
});
```

**Important:** This uses `IServiceProvider` (not `IServiceCollection`) through the `IConfigureOptions` pattern. This ensures:
- ✅ Proper service lifetimes
- ✅ No resource leaks
- ✅ Services are resolved at the correct time
- ✅ Integration with the options framework

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
/// Uses standard Microsoft.Extensions.Logging.LogLevel
/// </summary>
public LogLevel? MinimumLevelEnum { get; set; }
```

#### FileLoggingOptions

```csharp
/// <summary>
/// Rolling interval as enum (preferred for code-based configuration)
/// </summary>
public FileRollingInterval? RollingIntervalEnum { get; set; }
```

### New Extension Method

```csharp
/// <summary>
/// Add LogManager with access to IServiceProvider for resolving dependencies during configuration.
/// This uses IConfigureOptions pattern to safely access registered services.
/// </summary>
public static IServiceCollection AddLogManager(
    this IServiceCollection services,
    Action<LogManagerOptions, IServiceProvider> configureOptions)
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
opts.MinimumLevelEnum = LogLevel.   // IDE shows: Trace, Debug, Information, Warning, Error, Critical, None
```

### 2. Compile-Time Safety
Typos are caught at compile time:

```csharp
opts.MinimumLevel = "Informaton";      // ❌ Runtime error (typo)
opts.MinimumLevelEnum = LogLevel.Info; // ✅ Compile error (caught immediately)
```

### 3. Standard .NET Type
Uses the familiar `Microsoft.Extensions.Logging.LogLevel` that all .NET developers already know - no learning curve!

### 4. Service Provider Access
Configure LogManager based on other registered services:

```csharp
services.AddLogManager((opts, serviceProvider) =>
{
    // Fetch feature flags, environment config, etc.
    var featureFlags = serviceProvider.GetService<IFeatureFlags>();
    
    if (featureFlags?.EnableVerboseLogging == true)
    {
        opts.MinimumLevelEnum = LogLevel.Trace;  // Microsoft.Extensions.Logging.LogLevel.Trace
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
        RollingIntervalEnum = FileRollingInterval.Hour
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
