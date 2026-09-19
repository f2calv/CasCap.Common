# CasCap.Common.Net.Tests

xUnit tests for `CasCap.Common.Net`.

## Purpose

Verifies HTTP client base class behaviour and network extension methods — header parsing, query-string building, and `HttpClientBase` request/response handling.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Dependencies

### NuGet Packages

| Package |
| --- |
| [xunit.v3](https://www.nuget.org/packages/xunit.v3) |
| [Microsoft.Testing.Extensions.CodeCoverage](https://www.nuget.org/packages/microsoft.testing.extensions.codecoverage) |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Net` | Library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |

## Tests

| Test class | Methods | Test cases | Coverage |
| --- | --- | --- | --- |
| `HttpClientBaseTests` | 33 | 33 | `PostJsonAsync`, `PostBytesAsync`, `PostMultipartAsync`, `GetAsync` — success/error deserialization, headers, full-URL override, status/headers capture, raw string/bytes results, multipart part names and boundary, timeout & cancellation |
| `HttpClientBuilderResilienceExtensionTests` | 6 | 16 | `IsReplaySafe` — idempotent replay after timeout, non-idempotent refusal after timeout and 5xx, unsent-request exception, transmitted-request refusal, unknown request |
| `NetExtensionTests` | 16 | 16 | `ToQueryString`, `AddOrOverwrite` (string/list/dictionary), `TryGetValue`, `GetBasicAuthHeaderValue`, `SetBasicAuth` |
| **Total** | **55** | **65** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `HttpClientBase` | `HttpClientBaseTests` |
| `Resilience` | `HttpClientBuilderResilienceExtensionTests` |
| `Extensions` | `NetExtensionTests` |

### Skipped Tests

None.

## File Structure

```text
Tests/
├── ErrorPayload.cs
├── HttpClientBaseTests.cs
├── HttpClientBuilderResilienceExtensionTests.cs
├── MockHandler.cs
├── NetTests.cs
├── TestBase.cs
├── TestHttpClient.cs
└── TestPayload.cs
```
