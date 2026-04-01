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

| Package |
| --- |
| [Microsoft.Extensions.Hosting.Abstractions](https://www.nuget.org/packages/microsoft.extensions.hosting.abstractions) |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/microsoft.extensions.logging.abstractions) |
| [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/microsoft.extensions.logging.console) |
| [Microsoft.Extensions.Logging.Debug](https://www.nuget.org/packages/microsoft.extensions.logging.debug) |

This project has no project references.
