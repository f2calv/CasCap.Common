# CasCap.Common.Serialization.Tests

xUnit tests for `CasCap.Common.Serialization.Json` and `CasCap.Common.Serialization.MessagePack`.

## Purpose

Verifies JSON and MessagePack serialization round-trips, custom converter behaviour (epoch timestamps, 2-D arrays, string-to-int), and extension-method correctness.

**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Dependencies

### NuGet Packages

| Package | Version |
| --- | --- |
| `Microsoft.NET.Test.Sdk` | 18.3.0 |
| `xunit` | 2.9.3 |
| `xunit.runner.visualstudio` | 3.1.5 |
| `coverlet.collector` | 8.0.1 |
| `coverlet.msbuild` | 8.0.1 |

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Common.Serialization.Json` | JSON serialization library under test |
| `CasCap.Common.Serialization.MessagePack` | MessagePack serialization library under test |
| `CasCap.Common.Testing` | xUnit logging & skip attributes |
