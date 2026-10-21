# CasCap.Common.Serialization.MessagePack

MessagePack binary serialization helpers using the MessagePack-CSharp library.

## Installation

```bash
dotnet add package CasCap.Common.Serialization.MessagePack
```

**Target frameworks:** `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`

## Usage

```csharp
using System;
using System.Collections.Generic;
using CasCap.Common.Extensions;

var original = new Dictionary<string, string> { ["theme"] = "dark" };
byte[] bytes = original.ToMessagePack();
var restored = bytes.FromMessagePack<Dictionary<string, string>>();

Console.WriteLine(restored["theme"]); // dark
```

`ToMessagePack<T>()` returns a `byte[]`. `FromMessagePack<T>()` accepts either a `byte[]` or a
`ReadOnlyMemory<byte>` slice, avoiding a preliminary array copy when the caller already has a buffer.
Serialization and deserialization errors are logged and rethrown.

The helpers use the underlying serializer's default options. Custom object types must satisfy
[MessagePack-CSharp's serialization contract](https://github.com/MessagePack-CSharp/MessagePack-CSharp#serialization-of-custom-types).
For explicit resolver, security or serializer-option control, use `MessagePackSerializer` directly;
these helpers do not expose per-call options.

Treat serialized data as a contract between producer and consumer; do not assume arbitrary .NET
types can be serialized without configuration or that these helpers validate untrusted payloads.
