# CasCap.Common.Extensions.Tests

xUnit v3 tests for enum, string, date, parsing, collection, file and process helpers, plus configuration-source precedence.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Running

From the repository root with the .NET 10 SDK:

```bash
dotnet test --project src/CasCap.Common.Extensions.Tests/CasCap.Common.Extensions.Tests.csproj --framework net10.0
```

No external service or credentials are required. File tests create and remove files under the test
output's `cache` directory. Shell tests launch child processes, including `dotnet`; stdin round-trip
tests use `cmd.exe`/`more` on Windows or `cat` elsewhere. Linux-specific cases also need Bash and
`printf`. Run where those processes and temporary writes are permitted.

## Tests

Counts are declared methods and expanded data rows per target framework, before conditional skips.

| Test class | Methods | Test cases | Coverage |
| --- | --- | --- | --- |
| `ConfigurationBuilderExtensionsTests` | 1 | 1 | Standard configuration provider precedence |
| `EnumExtensionTests` | 8 | 11 | `GetAllItems`, `ParseEnum`, `TryParseEnum`, `ParseEnumFAST` (cache + cross-enum isolation), `ToStringCached`, `GetDisplayName`, `HasFlag` |
| `StringExtensionTests` | 13 | 21 | `ToSnakeCase`, `UrlCombine`, `String2List`, `SubstringSafe`, `Clean`, `IsEmail`, `ToBase64`, `Split`, `Sanitize`, `MaskPhoneNumber`, `MaskEndpoint` (incl. null), `NormalizeWhitespace` |
| `HelperExtensionTests` | 9 | 17 | `GetBatches`, `IsNullOrEmpty`/`IsAny`, `ToHashSet`, `ToBoolean`, `ToInt`, `ToDecimal`, `GetDescription`, XML & byte round-trips |
| `DateTimeExtensionTests` | 8 | 10 | `TruncateToHour`/`Day`/`Month`, `GetMissingDates`, `IsWeekend`, `ToUtc`, `To_yyyy_MM_dd`, `GetTimeDifference` |
| `FixedSizedQueueTests` | 4 | 4 | Eviction, `Clear`, constructor seeding & guard clauses |
| `BufferExtensionTests` | 2 | 2 | `TryReadLine` line splitting & no-newline behaviour |
| `ExtensionTests` | 2 | 2 | `UnixTimeMS`, `Decimal2Int` |
| `IOExtensionTests` | 1 | 1 | File read/write round-trip |
| `ShellExtensionTests` | 17 | 17 | `RunProcess`, `RunProcessDiagnostic`, `RunProcessWithStdinAsync` — success and failure paths, error capture modes, argument and stdin guards, oversized-payload round trip, cancellation, `Bash` platform guards |
| **Total** | **65** | **86** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `Enums` | `EnumExtensionTests`, `GetDescription` |
| `String Manipulation` | `StringExtensionTests` |
| `Masking` | `MaskPhoneNumber`, `MaskEndpoint` |
| `Validation` | `IsEmail` |
| `Parsing` | `BufferExtensionTests`, `ToBoolean`/`ToInt`/`ToDecimal`, `Decimal2Int` |
| `Dates` | `DateTimeExtensionTests` |
| `Collections` | `FixedSizedQueueTests`, `GetBatches`, `IsNullOrEmpty`, `ToHashSet` |
| `Serialization` | XML & byte round-trips |
| `IO` | `IOExtensionTests` |
| `Shell` | `ShellExtensionTests` |

### Skipped Tests

| Test | Cases | Skip condition |
| --- | ---: | --- |
| `ShellExtensionTests.Bash_OnLinux_ReturnsStandardOutput` | 1 | Non-Linux host |
| `ShellExtensionTests.RunProcessWithStdin_ArrayOverloadPreservesArgumentsContainingSpaces` | 1 | Non-Linux host; uses `printf` |
| `ShellExtensionTests.Bash_OffLinux_Throws` | 1 | Linux host; the unsupported-platform guard does not apply |

Two cases skip on Windows/non-Linux hosts; one skips on Linux. These are source-declared conditions,
not measured results from the current machine.

## File Structure

```text
Tests/
├── Unit/
│   └── ConfigurationBuilderExtensionsTests.cs
├── BufferExtensionTests.cs
├── DateTimeExtensionTests.cs
├── EnumExtensionTests.cs
├── ExtensionTests.cs
├── FixedSizedQueueTests.cs
├── HelperExtensionTests.cs
├── IOExtensionTests.cs
├── ShellExtensionTests.cs
├── StringExtensionTests.cs
└── TestBase.cs
```
