# CasCap.Common.Configuration

Configuration bootstrapping helpers for .NET applications — standard `IConfiguration` pipeline setup and validated `IOptions<T>` binding for `IAppConfig` records.

## Purpose

Provides a standardised way to build the configuration pipeline (appsettings.json, environment overrides, environment variables) and to bind configuration sections to `IAppConfig` record types with DataAnnotations validation on startup.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Extensions

| Class | Key Methods |
| --- | --- |
| `ConfigurationBuilderExtensions` | `AddStandardSources()` — registers `appsettings.json`, `appsettings.{env}.json`, and environment variables |
| `ConfigurationServiceCollectionExtensions` | `AddCasCapConfiguration<TConfig>()` — binds a configuration section to an `IAppConfig` record with `ValidateDataAnnotations` and `ValidateOnStart` |

## Dependencies

### NuGet Packages

| Package | Version |
| --- | --- |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 10.0.5 |
| `Microsoft.Extensions.Configuration.Json` | 10.0.5 |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | 10.0.5 |
| `Microsoft.Extensions.Options.DataAnnotations` | 10.0.5 |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Abstractions` | `IAppConfig` contract used as a generic constraint |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
