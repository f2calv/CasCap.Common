# CasCap.Common.Extensions

General-purpose extension methods and helper utilities for .NET applications.

## Purpose

Provides commonly-used extension methods for enums, parsing, I/O, collections, and XML — used as a foundational utility library across the CasCap ecosystem.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Extensions

| Class | Key Methods |
| --- | --- |
| `HelperExtensions` | `FromXml()`, `GetBatches()`, `ToConcurrentDictionary()`, `IsIntegration()`, `IsTest()` |
| `EnumExtensions` | `GetAllItems()`, `GetAllCombinations()`, `ToStringCached()`, `GetDisplayName()`, `HasFlag()` |
| `ParseExtensions` | `GetDecimalCount()`, `CsvStr2Date()`, `CsvDate2Str()`, `Decimal2Int()` |
| `IOExtensions` | `WriteAllBytes()`, `WriteAllTextAsync()`, `AppendTextFile()` |

### Exceptions

| Type | Description |
| --- | --- |
| `GenericException` | Generic catch-all exception type |

### Models

| Type | Description |
| --- | --- |
| `FixedSizedQueue<T>` | Fixed-capacity `ConcurrentQueue<T>` that auto-dequeues oldest items when full |

## Dependencies

### NuGet Packages

This project has no direct NuGet package references.

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
