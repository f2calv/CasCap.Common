# CasCap.Common.Logging.Serilog

Reusable Serilog configuration with standard enrichers, console sink, and health-check filtering built on top of `CasCap.Common.Logging`.

## Purpose

Provides a bootstrap logger for early startup logging and a composable `AddCasCapDefaults` extension on `LoggerConfiguration` that applies the standard CasCap Serilog pipeline: platform-aware console sink, common enrichers, health-check noise filter, and `appsettings.json` binding.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

### Extensions

| Extension | Description |
| --- | --- |
| `SerilogExtensions.GetBootstrapLogger()` | Creates a bootstrap console logger and wires `ApplicationLogging.LoggerFactory` |
| `LoggerConfiguration.AddCasCapDefaults(IConfiguration)` | Applies standard enrichers, console sink, health-check filter, and config binding |

### Enrichers Included

`FromLogContext`, `WithEnvironmentName`, `WithEnvironmentUserName`, `WithProcessId`, `WithThreadId`, `WithExceptionDetails`, `WithAssemblyName`, `WithSpan`

## Dependencies

### NuGet Packages

| Package | Version |
| --- | --- |
| `Serilog` | 4.3.1 |
| `Serilog.Enrichers.AssemblyName` | 2.0.0 |
| `Serilog.Enrichers.Environment` | 3.0.1 |
| `Serilog.Enrichers.Process` | 3.0.0 |
| `Serilog.Enrichers.Span` | 3.1.0 |
| `Serilog.Enrichers.Thread` | 4.0.0 |
| `Serilog.Exceptions` | 8.4.0 |
| `Serilog.Extensions.Hosting` | 10.0.0 |
| `Serilog.Extensions.Logging` | 10.0.0 |
| `Serilog.Settings.Configuration` | 10.0.0 |
| `Serilog.Sinks.Console` | 6.1.1 |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
