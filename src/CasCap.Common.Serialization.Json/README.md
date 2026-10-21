# CasCap.Common.Serialization.Json

`System.Text.Json` serialization helpers and custom `JsonConverter` implementations.

## Installation

```bash
dotnet add package CasCap.Common.Serialization.Json
```

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

## Usage

```csharp
using System;
using System.Collections.Generic;
using CasCap.Common.Extensions;

var original = new Dictionary<string, string> { ["theme"] = "dark" };
var json = original.ToJson();
var restored = json.FromJson<Dictionary<string, string>>();

Console.WriteLine(restored?["theme"]); // dark
```

`ToJson` and `FromJson<T>` accept optional `JsonSerializerOptions`. They log and rethrow
serialization errors. `TryFromJson<T>` logs a warning and returns `false` on deserialization failure;
a null input still throws. A valid JSON `null` can deserialize successfully to a null result.

## Custom Converters

Register converters explicitly on the options used for the call:

```csharp
using System;
using System.Text.Json;
using CasCap.Common.Converters;
using CasCap.Common.Extensions;

var options = new JsonSerializerOptions();
options.Converters.Add(new ParseEnumConverter<DayOfWeek>());

var day = "\"friday\"".FromJson<DayOfWeek>(options);
Console.WriteLine(day); // Friday
```

| Converter | Purpose |
| --- | --- |
| `Array2DConverter` | Rectangular two-dimensional arrays represented as nested JSON arrays |
| `MicrosecondEpochConverter` | Nullable `DateTime` values represented as Unix microseconds |
| `MillisecondEpochConverter` | Nullable `DateTime` values represented as Unix milliseconds |
| `ParseEnumConverter<TEnum>` | Case-insensitive enum names |
| `RawJsonStringConverter` | Embed valid JSON held in a string without double encoding; .NET 8+ only |
| `StringToIntConverter` | Nullable integers supplied as JSON strings |

`To2D()` also converts rectangular lists of lists into a two-dimensional array; inconsistent row
lengths throw. Conversion does not change the default `System.Text.Json` options globally.
