# CasCap.Common.Extensions.Tests

xUnit tests for `CasCap.Common.Extensions`.

## Purpose

Verifies general-purpose extension methods — enum helpers, parsing utilities, I/O operations, and collection extensions.

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
| `CasCap.Common.Configuration` | Standard configuration provider ordering |
| `CasCap.Common.Extensions` | Library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |

## Tests

| Test class | Methods | Test cases | Coverage |
| --- | --- | --- | --- |
| `ConfigurationBuilderExtensionsTests` | 1 | 1 | Standard configuration provider precedence |
| `EnumExtensionTests` | 8 | 11 | `GetAllItems`, `ParseEnum`, `TryParseEnum`, `ParseEnumFAST` (cache + cross-enum isolation), `ToStringCached`, `GetDisplayName`, `HasFlag` |
| `StringExtensionTests` | 13 | 21 | `ToSnakeCase`, `UrlCombine`, `String2List`, `SubstringSafe`, `Clean`, `IsEmail`, `ToBase64`, `Split`, `Sanitize`, `MaskPhoneNumber`, `MaskEndpoint` (incl. null), `NormalizeWhitespace` |
| `HelperExtensionTests` | 9 | 19 | `GetBatches`, `IsNullOrEmpty`/`IsAny`, `ToHashSet`, `ToBoolean`, `ToInt`, `ToDecimal`, `GetDescription`, XML & byte round-trips |
| `DateTimeExtensionTests` | 8 | 10 | `TruncateToHour`/`Day`/`Month`, `GetMissingDates`, `IsWeekend`, `ToUtc`, `To_yyyy_MM_dd`, `GetTimeDifference` |
| `FixedSizedQueueTests` | 4 | 4 | Eviction, `Clear`, constructor seeding & guard clauses |
| `BufferExtensionTests` | 2 | 2 | `TryReadLine` line splitting & no-newline behaviour |
| `ExtensionTests` | 2 | 2 | `UnixTimeMS`, `Decimal2Int` |
| `IOExtensionTests` | 1 | 1 | File read/write round-trip |
| `ShellExtensionTests` | 17 | 17 | `RunProcess`, `RunProcessDiagnostic`, `RunProcessWithStdinAsync` — success and failure paths, error capture modes, argument and stdin guards, oversized-payload round trip, cancellation, `Bash` platform guards |
| **Total** | **66** | **86** | |

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

| Test | Reason |
| --- | --- |
| `ShellExtensionTests.Bash_OnLinux_ReturnsStandardOutput` | Requires Linux |
| `ShellExtensionTests.RunProcessWithStdin_ArrayOverloadPreservesArgumentsContainingSpaces` | Requires `printf`, which reports each argument separately |

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
├── StringExtensionTests.cs
└── TestsBase.cs
```
