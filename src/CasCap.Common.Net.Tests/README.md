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
| [xunit.v3](https://www.nuget.org/packages/xunit.v3) |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector) |
| [coverlet.msbuild](https://www.nuget.org/packages/coverlet.msbuild) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Net` | Library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |

## Tests

| Test class | Methods | Test cases | Coverage |
| --- | --- | --- | --- |
| `HttpClientBaseTests` | 25 | 25 | `PostJsonAsync`, `PostBytesAsync`, `GetAsync` — success/error deserialization, headers, full-URL override, status/headers capture, raw string/bytes results, timeout & cancellation |
| `NetExtensionTests` | 18 | 18 | `ToQueryString`, `AddOrOverwrite` (string/list/dictionary), `TryGetValue`, `GetBasicAuthHeaderValue`, `SetBasicAuth` |
| **Total** | **43** | **43** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `HttpClientBase` | `HttpClientBaseTests` |
| `Extensions` | `NetExtensionTests` |

### Skipped Tests

None.

## File Structure

```text
Tests/
├── ErrorPayload.cs
├── HttpClientBaseTests.cs
├── MockHandler.cs
├── NetTests.cs
├── TestBase.cs
├── TestHttpClient.cs
└── TestPayload.cs
```
