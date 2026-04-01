# CasCap.Common.Net.Tests

xUnit tests for `CasCap.Common.Net`.

## Purpose

Verifies HTTP client base class behaviour and network extension methods — header parsing, query-string building, and `HttpClientBase` request/response handling.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Dependencies

### NuGet Packages

| Package |
| --- |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/microsoft.net.test.sdk) |
| [xunit](https://www.nuget.org/packages/xunit) |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector) |
| [coverlet.msbuild](https://www.nuget.org/packages/coverlet.msbuild) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Net` | Library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |
