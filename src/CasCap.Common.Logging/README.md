# CasCap.Common.Logging

Static logging abstraction via `ApplicationLogging`, providing a globally accessible `ILoggerFactory` for contexts where constructor injection is unavailable.

## Installation

```bash
dotnet add package CasCap.Common.Logging
```

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

## Usage

Connect the application's logging factory before calling helpers that create static loggers:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
using var host = builder.Build();
host.Services.AddStaticLogging();

var logger = ApplicationLogging.CreateLogger("Example");
logger.LogInformation("Application logging is ready");
```

Use `ILogger<T>` injection for ordinary services; the static factory is for code without constructor
injection. It starts as `NullLoggerFactory.Instance`, so logging before initialization is discarded.
Loggers already created from that null factory are not retroactively connected.

`ApplicationLogging.CreateLogger<T>()` creates a category from a type; `CreateLogger(string)` accepts
an explicit category. `AddStaticLogging()` requires an `ILoggerFactory` registered in the provider.
The application owns that factory's lifetime. The accessor is process-wide, so multiple hosts or
parallel tests must account for sharing it.

For ASP.NET Core Serilog setup, see
[CasCap.Common.Hosting.AspNetCore](https://github.com/f2calv/CasCap.Common/tree/main/src/CasCap.Common.Hosting.AspNetCore).
