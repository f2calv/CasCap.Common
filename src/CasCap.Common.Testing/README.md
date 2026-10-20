# CasCap.Common.Testing

xUnit v3 helpers for routing .NET logs to test output, stubbing HTTP responses, creating temporary files and conditionally skipping tests in CI.

## Installation

```bash
dotnet add package CasCap.Common.Testing
```

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Usage

### Capture Application Logs

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

public sealed class LoggingTests(ITestOutputHelper output)
{
    [Fact]
    public void CapturesLogs()
    {
        var services = new ServiceCollection();
        services.AddXUnitLogging(output);
        using var provider = services.BuildServiceProvider();

        var logger = provider.GetRequiredService<ILogger<LoggingTests>>();
        logger.LogInformation("Test logging is connected");

        Assert.IsType<TestLogProvider>(
            provider.GetRequiredService<ILoggerProvider>());
    }
}
```

`AddXUnitLogging` installs `TestLogProvider` at `Trace` level and connects the static
`ApplicationLogging` factory. That static factory is process-wide; tests must account for sharing it.

### Stub HTTP Without a Network

```csharp
using System.Net;
using System.Net.Http;
using CasCap.Common.Xunit;

using var handler = new StubHttpMessageHandler(_ =>
    new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("example")
    });
using var client = new HttpClient(handler);
var response = await client.GetStringAsync("https://example.com/resource");
```

The handler accepts synchronous or asynchronous response factories. Return a fresh response for
each request. It can also be assigned as a `DelegatingHandler.InnerHandler`.

### Temporary Files

`TempFile.Create()` creates an empty file. `CreateAsync` accepts text or bytes plus a cancellation
token. Both expose the generated `Path`; disposing the helper attempts to delete the file.

```csharp
using CasCap.Common.Xunit;

using var file = TempFile.Create(".json");
```

## Conditional CI Skips

| Attribute | Description |
| --- | --- |
| `SkipIfCIBuildFactAttribute` | Skips the `[Fact]` on Azure DevOps or GitHub Actions |
| `SkipIfCIBuildTheoryAttribute` | Skips the `[Theory]` on Azure DevOps or GitHub Actions |
| `SkipIfGithubActionsBuildFactAttribute` | Skips the `[Fact]` when running on GitHub Actions |
| `SkipIfGithubActionsBuildTheoryAttribute` | Skips the `[Theory]` when running on GitHub Actions |
| `SkipIfAzureDevOpsBuildFactAttribute` | Skips the `[Fact]` when running on Azure DevOps |
| `SkipIfAzureDevOpsBuildTheoryAttribute` | Skips the `[Theory]` when running on Azure DevOps |

These attributes live in `CasCap.Common.Xunit`. Their environment checks target the named CI
systems; they are not a general detector for every CI vendor.
