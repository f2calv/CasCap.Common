# CasCap.Common.Configuration.Blazor.Tests

.NET 10 bUnit component and typed-client tests for the embeddable runtime-configuration editor.
Tests use isolated in-memory HTTP handlers and need no PostgreSQL, Redis, credentials or browser.

## Running

From the repository root:

```powershell
dotnet test --project src\CasCap.Common.Configuration.Blazor.Tests\CasCap.Common.Configuration.Blazor.Tests.csproj --configuration Release
```

| Class | Expanded cases | Coverage |
| --- | --- | --- |
| `RuntimeConfigurationEditorTests` | 20 | Typed boolean/enum/number/duration editing, preview/confirmation, invalid values/metadata, pending activation, dirty reload, safe states, retained edits, no retry, labels/form actions and cancellation |
| `RuntimeConfigurationClientTests` | 4 | Exact 64-bit revision/typed wire, antiforgery headers, malformed response/transport failure, browser assembly dependency boundary |
| Total | 24 | |

Observed local Release result: 24 passed, zero failures or skips.
Fixtures dispose rendered components and HTTP clients, including pending requests.
The Razor harness supplies typed fields and DTO annotations; it is not a production login page.

The [provider/HTTP suite](../CasCap.Common.Configuration.Runtime.Tests/README.md) additionally
tests this real typed client against authenticated `WebApplicationFactory`/SQLite endpoints:
cookie plus antiforgery, preview/no write, publish, conflict, sign-in and permission failures.
It also tests actual PostgreSQL/Redis provider and stateless replica behavior.

These are component/client tests, not a real browser, WebAssembly download, production identity
provider or live deployment test. A consuming host still owns its navigation, rendering,
Data Protection, authorization and browser/accessibility checks.
