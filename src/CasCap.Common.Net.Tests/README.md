# CasCap.Common.Net.Tests

xUnit tests for `CasCap.Common.Net`.

## Purpose

Verifies HTTP client base class behaviour and network extension methods — header parsing, query-string building, and `HttpClientBase` request/response handling.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Dependencies

### NuGet Packages

| Package | Version |
| --- | --- |
| `Microsoft.NET.Test.Sdk` | 18.3.0 |
| `xunit` | 2.9.3 |
| `xunit.runner.visualstudio` | 3.1.5 |
| `coverlet.collector` | 8.0.1 |
| `coverlet.msbuild` | 8.0.1 |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Net` | Library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |
