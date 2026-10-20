# CasCap.Common.Extensions

General-purpose extension methods and helper utilities for .NET applications.

## Installation

```bash
dotnet add package CasCap.Common.Extensions
```

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

## Usage

```csharp
using System;
using CasCap.Common.Extensions;

var columnName = "OrderTotal".ToSnakeCase();
var endpoint = "https://example.com/api/".UrlCombine("/items");

Console.WriteLine(columnName); // order_total
Console.WriteLine(endpoint);   // https://example.com/api/items
```

## Available Helpers

| Class | Key Methods |
| --- | --- |
| `DateTimeExtensions` | Date truncation, Unix time, missing dates and weekday calculations |
| `StringExtensions` | Case conversion, URL combination, validation, masking and whitespace normalization |
| `HelperExtensions` | Collections, XML, byte conversion and compression |
| `EnumExtensions` | Enum enumeration, parsing, cached names and display names |
| `ParseExtensions` | Numeric conversions and CSV date parsing |
| `IOExtensions` | File creation, writing and appending |
| `ShellExtensions` | Process execution, binary stdin/stdout and Linux Bash commands |
| `BufferExtensions` | `TryReadLine()` for sequence buffers; .NET 8+ only |

`FixedSizedQueue<T>` provides a fixed-capacity concurrent queue that evicts the oldest items.

## Piping Binary Data Through a Process

`RunProcessWithStdinAsync` writes bytes to a child process and reads its standard output back, which
suits in-memory media transcoding without temporary files. Both output streams are drained while the
input is written, so a payload larger than the pipe buffer cannot deadlock, and the process is killed
along with its children whenever the method stops waiting on it.

Pass each argument as its own array element; nothing is quoted or escaped on the way through:

```csharp
using System;
using CasCap.Common.Extensions;
using CasCap.Common.Models;

var result = await ShellExtensions.RunProcessWithStdinAsync(
    "ffmpeg",
    ["-i", "pipe:0", "-f", "wav", "-ar", "16000", "pipe:1"],
    inputBytes,
    ProcessErrorCapture.Length);

if (!result.Success)
    throw new InvalidOperationException($"Transcoding failed with exit code {result.ExitCode}.");

byte[] audioBytes = result.Output;
```

The example assumes `inputBytes` contains the media to convert and `ffmpeg` is installed. Choose
`ProcessErrorCapture.Length` when diagnostics may contain third-party content: `Error` is discarded,
while `ErrorLength` records its length. `ProcessErrorCapture.Text` retains the diagnostic string.
`ProcessResult.Success` reflects the exit code; cancellation propagates to the caller.

`Bash()` is supported only on Linux. Other process helpers require the named executable on the host.
