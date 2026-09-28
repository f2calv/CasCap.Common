# CasCap.Common.AI.Tests

Unit tests for [CasCap.Common.AI](../CasCap.Common.AI) and [CasCap.Common.AI.Evaluation](../CasCap.Common.AI.Evaluation).

## Running

The repository uses the .NET 10 native Microsoft.Testing.Platform runner:

```powershell
dotnet test --project src/CasCap.Common.AI.Tests/CasCap.Common.AI.Tests.csproj
```

## Tests

| Class | Methods | Cases | Covers |
| --- | --- | --- | --- |
| `ToolOutputStrippingChatReducerTests` | 11 | 17 | Tool-content stripping, orphaned tool-call prevention, sliding window, system-message retention, metadata preservation, input immutability, argument validation |
| `AgentExtensionsCreateAgentTests` | 9 | 13 | Provider validation for Ollama / Azure OpenAI / OpenAI endpoints and credentials, unsupported provider types |
| `AgentCommandHandlerTests` | 15 | 18 | Per-agent isolation of `/model`, `/instructions` and `/session enable\|disable` overrides, instruction prefix/suffix wrapping, session short-circuiting, case-insensitive agent keys |
| `AgentResponseUsageTests` | 5 | 5 | Framework usage aggregation across tool-call round-trips, `RunAnalysisAsync` usage/tool-call reporting |
| `AgentTelemetryTests` | 3 | 3 | Agent-level OpenTelemetry spans, sub-agent span nesting, sensitive-data opt-in |
| `AgentRunScopeTests` | 8 | 10 | Depth nesting, callback inheritance, shared/thread-safe attachment collection, drain semantics |
| `AgentTypeRegistryTests` | 11 | 11 | Registry lookup, ambiguous-name rejection, DI and assembly indexing, tool resolution via registry |
| `AgentEvaluationGradingTests` | 7 | 18 | Number, phrase, anchored-pattern and combined answer checks, required/forbidden/side-effect grading, Wilson intervals |
| `AgentEvaluationToolTests` | 11 | 18 | Tool classification and filtering, fixture and sandbox responses, variant descriptions and schemas, harness tool assembly and provider availability, MCP prompt contract |
| `AgentEvaluationReportTests` | 5 | 7 | Per-cell and cross-model summaries, speed ratios, session files, visible-thinking detection |
| **Total** | **85** | **120** | |

## Trait Categories

| Category | Applied to |
| --- | --- |
| `Chat Reduction` | `ToolOutputStrippingChatReducerTests` |
| `Agent Creation` | `AgentExtensionsCreateAgentTests` |
| `Agent Commands` | `AgentCommandHandlerTests` |
| `Usage Reporting` | `AgentResponseUsageTests` |
| `Telemetry` | `AgentTelemetryTests` |
| `Agent Run Scope` | `AgentRunScopeTests` |
| `Type Resolution` | `AgentTypeRegistryTests` |
| `Agent Evaluation` | `AgentEvaluationGradingTests`, `AgentEvaluationToolTests`, `AgentEvaluationReportTests` |

## Skipped Tests

None.

## File Structure

```text
Tests/
├── AgentEvaluationTestData.cs
└── Unit/
    ├── AgentCommandHandlerTests.cs
    ├── AgentEvaluationGradingTests.cs
    ├── AgentEvaluationReportTests.cs
    ├── AgentEvaluationToolTests.cs
    ├── AgentExtensionsCreateAgentTests.cs
    ├── AgentRunScopeTests.cs
    ├── AgentResponseUsageTests.cs
    ├── AgentTelemetryTests.cs
    ├── AgentTypeRegistryTests.cs
    └── ToolOutputStrippingChatReducerTests.cs
```

## Notes

`ReduceAsync_MixedTextAndToolCall_NoOrphanedToolCall` is a regression test. An assistant
message mixing `TextContent` with `FunctionCallContent` was previously retained while its
matching `FunctionResultContent` message was dropped, leaving an orphaned tool call that
OpenAI and Azure OpenAI reject with HTTP 400.

The `*_IsolatedPerAgent` tests in `AgentCommandHandlerTests` are also regression tests.
`AgentCommandHandler` is registered as a singleton, so the previous single-field override
state meant a `/model` sent in one conversation re-pointed every other agent in the process.

`AgentResponseUsageTests` pins framework behaviour rather than library behaviour:
`AgentExtensions` previously carried an ambient accumulator because the agent framework did
not surface per-round-trip usage. It now aggregates onto `AgentResponse.Usage`, so these
tests guard the assumption that allowed that accumulator to be deleted.
