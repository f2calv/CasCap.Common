# CasCap.Common.Serialization.Tests

xUnit tests for `CasCap.Common.Serialization.Json` and `CasCap.Common.Serialization.MessagePack`.

## Purpose

Verifies JSON and MessagePack serialization round-trips, custom converter behaviour (epoch timestamps, 2-D arrays, string-to-int), and extension-method correctness.

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
| `CasCap.Common.Serialization.Json` | JSON serialization library under test |
| `CasCap.Common.Serialization.MessagePack` | MessagePack serialization library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |

## Tests

| Test class | Methods | Test cases | Coverage |
| --- | --- | --- | --- |
| `JsonConverterTests` | 11 | 15 | `Array2DConverter`, `MicrosecondEpochConverter`, `MillisecondEpochConverter`, `ParseEnumConverter<TEnum>`, `RawJsonStringConverter`, `StringToIntConverter` |
| `JsonTests` | 1 | 1 | `ToJson`/`FromJson` round-trips & error handling |
| `MessagePackTests` | 1 | 1 | `ToMessagePack`/`FromMessagePack` round-trips |
| **Total** | **13** | **17** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `Serialization` | `JsonConverterTests` |

`JsonTests` and `MessagePackTests` carry no trait category.

### Skipped Tests

None.

## File Structure

```text
Tests/
├── JsonConverterTests.cs
├── JsonTests.cs
├── MessagePackTests.cs
└── TestBase.cs
```
