# CasCap.Common.Serialization.Tests

xUnit v3 tests for JSON and MessagePack round-trips, epoch and enum converters, rectangular arrays and string-to-integer conversion.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Running

From the repository root with the .NET 10 SDK:

```bash
dotnet test --project src/CasCap.Common.Serialization.Tests/CasCap.Common.Serialization.Tests.csproj --framework net10.0
```

The suite is self-contained; no credentials, network service or external data files are required.

## Tests

Counts are declared methods and expanded data rows per target framework, not execution results.

| Test class | Methods | Test cases | Coverage |
| --- | --- | --- | --- |
| `JsonConverterTests` | 10 | 15 | `Array2DConverter`, `MicrosecondEpochConverter`, `MillisecondEpochConverter`, `ParseEnumConverter<TEnum>`, `RawJsonStringConverter`, `StringToIntConverter` |
| `JsonTests` | 1 | 1 | `ToJson`/`FromJson` round-trips & error handling |
| `MessagePackTests` | 1 | 1 | `ToMessagePack`/`FromMessagePack` round-trips |
| **Total** | **12** | **17** | |

### Trait Categories

| Category | Used by |
| --- | --- |
| `Serialization` | `JsonConverterTests` |

`JsonTests` and `MessagePackTests` carry no trait category.

### Skipped Tests

No declared or conditional skips.

## File Structure

```text
Tests/
├── JsonConverterTests.cs
├── JsonTests.cs
├── MessagePackTests.cs
└── TestBase.cs
```
