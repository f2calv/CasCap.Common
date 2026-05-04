# CasCap.Common.Logging.Serilog

Reusable Serilog configuration with standard enrichers, console sink, and health-check filtering built on top of `CasCap.Common.Logging`.

## Purpose

Provides a bootstrap logger for early startup logging and a composable `AddCasCapDefaults` extension on `LoggerConfiguration` that applies the standard CasCap Serilog pipeline: platform-aware console sink, common enrichers, health-check noise filter, and `appsettings.json` binding.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

### Extensions

| Extension | Description |
| --- | --- |
| `SerilogExtensions.GetBootstrapLogger()` | Creates a bootstrap console logger and wires `ApplicationLogging.LoggerFactory` |
| `SerilogWebApplicationBuilderExtensions.InitializeSerilog(builder, categoryName)` | Thread-safe one-shot Serilog initialization on a `WebApplicationBuilder` via `UseSerilog` + `AddCasCapDefaults` |
| `LoggerConfiguration.AddCasCapDefaults(IConfiguration)` | Applies standard enrichers, console sink, health-check filter, and config binding |

### Enrichers Included

`FromLogContext`, `WithEnvironmentName`, `WithEnvironmentUserName`, `WithProcessId`, `WithThreadId`, `WithExceptionDetails`, `WithAssemblyName`, `WithSpan`

## Dependencies

### NuGet Packages

| Package |
| --- |
| [Serilog](https://www.nuget.org/packages/serilog) |
| [Serilog.Enrichers.AssemblyName](https://www.nuget.org/packages/serilog.enrichers.assemblyname) |
| [Serilog.Enrichers.Environment](https://www.nuget.org/packages/serilog.enrichers.environment) |
| [Serilog.Enrichers.Process](https://www.nuget.org/packages/serilog.enrichers.process) |
| [Serilog.Enrichers.Span](https://www.nuget.org/packages/serilog.enrichers.span) |
| [Serilog.Enrichers.Thread](https://www.nuget.org/packages/serilog.enrichers.thread) |
| [Serilog.Exceptions](https://www.nuget.org/packages/serilog.exceptions) |
| [Serilog.Extensions.Hosting](https://www.nuget.org/packages/serilog.extensions.hosting) |
| [Serilog.Extensions.Logging](https://www.nuget.org/packages/serilog.extensions.logging) |
| [Serilog.Settings.Configuration](https://www.nuget.org/packages/serilog.settings.configuration) |
| [Serilog.Sinks.Console](https://www.nuget.org/packages/serilog.sinks.console) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
