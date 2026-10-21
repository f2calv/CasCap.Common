# CasCap.Common.Services

Run selected .NET background features by name, observe their lifecycle and expose assembly-backed application metadata.

## Installation

```bash
dotnet add package CasCap.Common.Services
```

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Usage

Register implementations as
[`IBgFeature`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IBgFeature.cs),
then supply the enabled names to the launcher:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CasCap.Common.Abstractions;
using CasCap.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IBgFeature, WorkerFeature>();
builder.Services.AddFeatureFlagService(
    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Worker" });

await builder.Build().RunAsync();

public sealed class WorkerFeature(ILogger<WorkerFeature> logger) : IBgFeature
{
    public string FeatureName => "Worker";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Worker is active");
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }
}
```

Feature matching is case-insensitive. A feature named `IBgFeature.AlwaysEnabled` (`"All"`) runs
regardless of the selected set. Registration supplies `IOptions<FeatureFlagConfig>`; it does not
automatically read an `EnabledFeatures` JSON section.

## Service Architecture

- No matching features is a startup failure.
- A child may finish while siblings continue. Faults from enabled children propagate to the host.
- If all children finish before cancellation, the launcher reports an unexpected lifecycle failure.
- A finite job must request `IHostApplicationLifetime.StopApplication()` before returning.
- Host cancellation ends the launcher cleanly.

```mermaid
flowchart TD
    HOST["Host startup"] --> MATCH["Match registered features against enabled names"]
    MATCH --> RUN["Run matching features and All features"]
    RUN --> WATCH["Observe child tasks"]
    WATCH -->|Child finishes, siblings remain| WATCH
    WATCH -->|Child faults or all finish without stopping| FAIL["Propagate failure to host"]
    WATCH -->|Host cancellation or requested stop| STOP["Clean shutdown"]
```

## Configuration Examples

### Feature Execution Only

The registration in Usage enables the selected features without periodic metadata logging.

### Feature Execution with Metadata

Pass `addApplicationMetadataService: true` to register `ApplicationMetadataBgService` and
`ApplicationMetadata`. The latter reads version, source revision and build context from assembly
attributes, not live Git state.

```csharp
builder.Services.AddFeatureFlagService(enabledFeatures, addApplicationMetadataService: true);
```

The optional metadata service binds `ApplicationMetadataConfig`. Its `LogInterval` defaults to one
minute and must be positive:

```json
{
  "ApplicationMetadataConfig": {
    "LogInterval": "00:05:00"
  }
}
```
