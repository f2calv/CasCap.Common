# CasCap.Common.Extensions

General-purpose extension methods and helper utilities for .NET applications.

## Installation

```bash
dotnet add package CasCap.Common.Extensions
```

## Purpose

Provides commonly-used extension methods for date/time, strings, enums, parsing, I/O, buffers, collections, and XML — used as a foundational utility library across the CasCap ecosystem.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Extensions

| Class | Key Methods |
| --- | --- |
| [`DateTimeExtensions`](Extensions/DateTimeExtensions.cs) | `TruncateToHour()`, `TruncateToDay()`, `TruncateToMonth()`, `GetMissingDates()`, `FromUnixTime()`, `ToUnixTime()`, `IsWeekend()`, `AddWeekdays()`, `ToRelativeDateString()` |
| [`StringExtensions`](Extensions/StringExtensions.cs) | `ToSnakeCase()`, `UrlCombine()`, `IsEmail()`, `MaskPhoneNumber()`, `MaskEndpoint()`, `NormalizeWhitespace()`, `Sanitize()` |
| [`HelperExtensions`](Extensions/HelperExtensions.cs) | `FromXml()`, `GetBatches()`, `ToConcurrentDictionary()`, `IsIntegration()`, `IsTest()`, `Compress()`, `Decompress()` |
| [`EnumExtensions`](Extensions/EnumExtensions.cs) | `GetAllItems()`, `GetAllCombinations()`, `ToStringCached()`, `GetDisplayName()`, `HasFlag()` |
| [`ParseExtensions`](Extensions/ParseExtensions.cs) | `GetDecimalCount()`, `CsvStr2Date()`, `CsvDate2Str()`, `Decimal2Int()`, `Decimal2Long()` |
| [`IOExtensions`](Extensions/IOExtensions.cs) | `WriteAllBytes()`, `WriteAllTextAsync()`, `AppendTextFile()` |
| [`ShellExtensions`](Extensions/ShellExtensions.cs) | `RunProcess()`, `RunProcessDiagnostic()`, `Bash()` |
| [`BufferExtensions`](Extensions/BufferExtensions.cs) | `TryReadLine()` (NET8+) |

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
