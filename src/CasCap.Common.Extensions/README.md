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
| [`ShellExtensions`](Extensions/ShellExtensions.cs) | `RunProcess()`, `RunProcessDiagnostic()`, `RunProcessWithStdinAsync()`, `Bash()` |
| [`BufferExtensions`](Extensions/BufferExtensions.cs) | `TryReadLine()` (NET8+) |

### Exceptions

| Type | Description |
| --- | --- |
| `GenericException` | Generic catch-all exception type |

### Models

| Type | Description |
| --- | --- |
| `FixedSizedQueue<T>` | Fixed-capacity `ConcurrentQueue<T>` that auto-dequeues oldest items when full |
| `ProcessResult` | Readonly record struct returned by `RunProcessWithStdinAsync` — `Output`, `Error`, `ErrorLength`, `ExitCode`, and `Success` |
| `ProcessErrorCapture` | Whether standard error is retained as `Text` or counted and discarded as `Length` |

## Piping Binary Data Through a Process

`RunProcessWithStdinAsync` writes bytes to a child process and reads its standard output back, which
suits in-memory media transcoding without temporary files. Both output streams are drained while the
input is written, so a payload larger than the pipe buffer cannot deadlock, and the process is killed
along with its children whenever the method stops waiting on it.

Pass each argument as its own array element; nothing is quoted or escaped on the way through:

```csharp
using CasCap.Common.Models;

var result = await ShellExtensions.RunProcessWithStdinAsync(
    "ffmpeg",
    ["-i", "pipe:0", "-f", "wav", "-ar", "16000", "pipe:1"],
    inputBytes,
    ProcessErrorCapture.Length);

if (result.Success)
    Use(result.Output);
```

Choose `ProcessErrorCapture.Length` when the diagnostics may echo third-party file names or other
content that should not be retained; the count still proves whether anything was written.

The overload taking a single `arguments` string is obsolete because it splits on spaces, so any
argument containing one arrives as several.

## Dependencies

### NuGet Packages

This project has no direct NuGet package references.

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
