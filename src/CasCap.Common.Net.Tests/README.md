# CasCap.Common.Net.Tests

xUnit v3 tests for typed HTTP request/response handling, retry safety and network extensions in `CasCap.Common.Net`.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Running

From the repository root with the .NET 10 SDK:

```bash
dotnet test --project src/CasCap.Common.Net.Tests/CasCap.Common.Net.Tests.csproj --framework net10.0
```

The HTTP tests use local stub handlers, not live API endpoints. No credentials or external service
are required.

## Tests

Counts are declared methods and expanded data rows per target framework, not execution results.
The resilience theory sources contribute six idempotent methods, two non-idempotent methods per
applicable theory, and four pre-transmission failure cases.

| Test class | Methods | Test cases | Coverage |
| --- | --- | --- | --- |
| `HttpClientBaseTests` | 33 | 33 | `PostJsonAsync`, `PostBytesAsync`, `PostMultipartAsync`, `GetAsync` — success/error deserialization, headers, full-URL override, status/headers capture, raw string/bytes results, multipart part names and boundary, timeout & cancellation |
| `HttpClientBuilderResilienceExtensionTests` | 6 | 16 | `IsReplaySafe` — idempotent replay after timeout, non-idempotent refusal after timeout and 5xx, unsent-request exception, transmitted-request refusal, unknown request |
| `HttpAuditHandlerTests` | 5 | 18 | Empty, deep JSON/scalar, text/HTML and malformed-body normalization; unchanged HTTP content/status, source metadata, persistence warnings and caller cancellation |
| `NetExtensionTests` | 16 | 16 | `ToQueryString`, `AddOrOverwrite` (string/list/dictionary), `TryGetValue`, `GetBasicAuthHeaderValue`, `SetBasicAuth` |
| **Total** | **60** | **83** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `HttpClientBase` | `HttpClientBaseTests` |
| `Resilience` | `HttpClientBuilderResilienceExtensionTests` |
| `Extensions` | `NetExtensionTests` |
| `Auditing` | `HttpAuditHandlerTests` |

### Skipped Tests

No declared or conditional skips.

## File Structure

```text
Tests/
├── ErrorPayload.cs
├── HttpClientBaseTests.cs
├── HttpClientBuilderResilienceExtensionTests.cs
├── MockHandler.cs
├── NetExtensionTests.cs
├── TestBase.cs
├── TestHttpClient.cs
├── TestPayload.cs
└── Unit/
    └── HttpAuditHandlerTests.cs
```
