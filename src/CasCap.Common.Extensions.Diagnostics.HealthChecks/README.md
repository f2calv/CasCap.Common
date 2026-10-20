# CasCap.Common.Extensions.Diagnostics.HealthChecks

Custom ASP.NET Core health check base classes for monitoring HTTP endpoint availability.

## Installation

```bash
dotnet add package CasCap.Common.Extensions.Diagnostics.HealthChecks
```

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

## Usage

Implement `IHealthCheckConfig` and supply it to a derived check. This ASP.NET Core example
registers an HTTP client and exposes the check through the standard health endpoint:

```csharp
using System.Collections.Generic;
using System.Net.Http;
using CasCap.Common.Diagnostics.HealthChecks;
using CasCap.Common.Diagnostics.HealthChecks.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IHealthCheckConfig>(new EndpointConfig());
builder.Services.AddHttpClient<EndpointHealthCheck>();
builder.Services.AddHealthChecks().AddCheck<EndpointHealthCheck>("upstream");

var app = builder.Build();
app.MapHealthChecks("/healthz");
app.Run();

public sealed record EndpointConfig : IHealthCheckConfig
{
    public string HealthCheckUri { get; init; } = "https://example.com/health";
    public IReadOnlyList<int> HealthCheckExpectedHttpStatusCodes { get; init; } = [200];
}

public sealed class EndpointHealthCheck(
    ILogger<EndpointHealthCheck> logger,
    HttpClient client,
    IHealthCheckConfig config) : HttpEndpointCheckBase(logger, client, config, "Upstream");
```

Replace the example URI with the caller's endpoint through application configuration. The base
class sends a GET and reports healthy only for an expected status; `ConnectionActive` tracks the
last result. Configure timeouts on the injected `HttpClient`.

## Configuration Examples

The library does not bind a fixed JSON section. Your application supplies `IHealthCheckConfig`,
either directly as above or by binding its own options.

| Property | Minimal value | Customized example |
| --- | --- |
| `HealthCheckUri` | Required endpoint URI | `https://example.com/ready` |
| `HealthCheckExpectedHttpStatusCodes` | `[200]` | `[200, 204]` |

On .NET Standard 2.0, implement both properties. On .NET 8+, the interface supplies `[200]` when the
status list is not overridden.

## Kubernetes Probe Tags

`KubernetesProbeTypes.GetTags()` maps probe flags to `ready`, `live` and `startup` strings for use
with health-check registration and endpoint predicates. Tagging a check does not create Kubernetes
probes or configure HTTP health endpoints.
