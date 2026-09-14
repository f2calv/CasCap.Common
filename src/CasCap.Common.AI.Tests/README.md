# CasCap.Common.AI.Tests

Unit tests for [CasCap.Common.AI](../CasCap.Common.AI).

## Running

The repo's xUnit v3 projects build as executables and host the in-process runner, so run the
test assembly directly:

```powershell
dotnet build src/CasCap.Common.AI.Tests/CasCap.Common.AI.Tests.csproj
./src/CasCap.Common.AI.Tests/bin/Debug/net10.0/CasCap.Common.AI.Tests.exe
```

> `dotnet test` currently reports `Zero tests ran` for every xUnit v3 project in this
> repository (a CLI/Microsoft.Testing.Platform bridging quirk, not a project-specific fault).

## Tests

| Class | Methods | Cases | Covers |
| --- | --- | --- | --- |
| `ToolOutputStrippingChatReducerTests` | 11 | 17 | Tool-content stripping, orphaned tool-call prevention, sliding window, system-message retention, metadata preservation, input immutability, argument validation |
| `AgentExtensionsCreateAgentTests` | 8 | 9 | Provider validation for Ollama / Azure OpenAI / OpenAI endpoints and credentials, unsupported provider types |

## Trait Categories

| Category | Applied to |
| --- | --- |
| `Chat Reduction` | `ToolOutputStrippingChatReducerTests` |
| `Agent Creation` | `AgentExtensionsCreateAgentTests` |

## Skipped Tests

None.

## File Structure

```text
Tests/
└── Unit/
    ├── AgentExtensionsCreateAgentTests.cs
    └── ToolOutputStrippingChatReducerTests.cs
```

## Notes

`ReduceAsync_MixedTextAndToolCall_NoOrphanedToolCall` is a regression test. An assistant
message mixing `TextContent` with `FunctionCallContent` was previously retained while its
matching `FunctionResultContent` message was dropped, leaving an orphaned tool call that
OpenAI and Azure OpenAI reject with HTTP 400.
