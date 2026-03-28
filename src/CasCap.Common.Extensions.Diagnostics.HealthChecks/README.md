# CasCap.Common.Extensions.Diagnostics.HealthChecks

Custom ASP.NET Core health check base classes for monitoring HTTP endpoint availability.

## Purpose

Provides `HttpEndpointCheckBase`, an abstract `IHealthCheck` that simplifies writing health checks for external HTTP endpoints. Derived classes only need to supply the target URL; the base class handles the HTTP call, timeout, and result mapping.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Health Checks

| Type | Description |
| --- | --- |
| `HttpEndpointCheckBase` | Abstract base class — accepts `ILogger` and `HttpClient`, exposes `IsAccessible()` for derived implementations |

## Dependencies

### NuGet Packages

| Package | Version |
| --- | --- |
| `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` | 10.0.5 |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
