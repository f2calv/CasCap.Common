# CasCap.Common.Serialization.MessagePack

MessagePack binary serialization helpers using the MessagePack-CSharp library.

## Purpose

Provides `ToMessagePack<T>()` and `FromMessagePack<T>()` extension methods for fast, compact binary serialization. The MessagePack analyzer is included as a development dependency to enforce correct attribute usage at build time.

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

### Extensions

| Class | Key Methods |
| --- | --- |
| `MessagePackExtensions` | `ToMessagePack<T>()` — serialize to `byte[]`; `FromMessagePack<T>()` — deserialize from `byte[]` |

## Dependencies

### NuGet Packages

| Package | Version |
| --- | --- |
| `MessagePack` | 3.1.4 |
| `MessagePackAnalyzer` | 3.1.4 |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Logging` | `ApplicationLogging` static logger factory |
