# CasCap.Common.Logging

Static logging abstraction via `ApplicationLogging`, providing a globally accessible `ILoggerFactory` for contexts where constructor injection is unavailable.

## Purpose

Exposes a static `ApplicationLogging` class that holds an `ILoggerFactory` reference and `CreateLogger` convenience methods. An `AddStaticLogging()` extension on `IServiceProvider` wires the DI-registered factory into the static accessor at application startup.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Services

| Type | Description |
| --- | --- |
| `ApplicationLogging` | Static class exposing `LoggerFactory` property and `CreateLogger<T>()` / `CreateLogger(string)` methods |

### Extensions

| Extension | Description |
| --- | --- |
| `ServiceProviderExtensions.AddStaticLogging()` | Assigns the DI `ILoggerFactory` to `ApplicationLogging.LoggerFactory` |

## Dependencies

### NuGet Packages

| Package | Version |
| --- | --- |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.5 |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.5 |
| `Microsoft.Extensions.Logging.Console` | 10.0.5 |
| `Microsoft.Extensions.Logging.Debug` | 10.0.5 |

This project has no project references.
