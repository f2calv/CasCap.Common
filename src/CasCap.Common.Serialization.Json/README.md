# CasCap.Common.Serialization.Json

`System.Text.Json` serialization helpers and custom `JsonConverter` implementations.

## Purpose

Provides convenient `ToJson()` / `FromJson()` extension methods and a library of custom converters for common serialization scenarios such as epoch timestamps, 2-D array round-tripping, and string-to-int coercion.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Extensions

| Class | Key Methods |
| --- | --- |
| `JsonExtensions` | `ToJson()`, `FromJson()`, `TryFromJson()`, `To2D()` (jagged ↔ 2-D array conversion) |

### Converters

| Converter | Description |
| --- | --- |
| `Array2DConverter` | Round-trips 2-D arrays via a jagged-array intermediary |
| `ColumnCleanerConverter` | Strips unwanted characters from column names during deserialization |
| `MicrosecondEpochConverter` | Converts microsecond Unix epoch timestamps to `DateTime` |
| `MillisecondEpochConverter` | Converts millisecond Unix epoch timestamps to `DateTime` |
| `StringToIntConverter` | Coerces JSON string values to `int` |
| `UtcJsonDateTimeConverter` | Ensures `DateTime` values are always treated as UTC |

## Dependencies

### NuGet Packages

This project has no direct NuGet package references.

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Extensions` | General-purpose helper utilities |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
