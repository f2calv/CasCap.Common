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
| `AgentExtensionsCreateAgentTests` | 10 | 14 | Provider validation for Ollama / Azure OpenAI / OpenAI endpoints and credentials, caller-supplied transport, unsupported provider types |
| `ToolSourceValidationTests` | 2 | 2 | Remote MCP logical credential references are valid only with endpoint sources |
| `AgentResponseUsageTests` | 5 | 5 | Framework usage aggregation across tool-call round-trips, `RunAnalysisAsync` usage/tool-call reporting |
| `AgentTelemetryTests` | 3 | 3 | Agent-level OpenTelemetry spans, sub-agent span nesting, sensitive-data opt-in |
| `AgentRunScopeTests` | 8 | 10 | Depth nesting, callback inheritance, shared/thread-safe attachment collection, drain semantics |
| `AgentTypeRegistryTests` | 11 | 11 | Registry lookup, ambiguous-name rejection, DI and assembly indexing, tool resolution via registry |
| `AgentEvaluationGradingTests` | 7 | 18 | Number, phrase, anchored-pattern and combined answer checks, required/forbidden/side-effect grading, Wilson intervals |
| `AgentEvaluationToolTests` | 11 | 18 | Tool classification and filtering, fixture and sandbox responses, variant descriptions and schemas, harness tool assembly and provider availability, MCP prompt contract |
| `AgentEvaluationReportTests` | 5 | 7 | Per-cell and cross-model summaries, speed ratios, session files, visible-thinking detection |
| `LlamaCppIntegrationTests` | 4 | 4 | Opt-in real text, streaming, multimodal, and framework-session inference against local llama.cpp |
| **Total** | **77** | **109** | |

## Trait Categories

| Category | Applied to |
| --- | --- |
| `Chat Reduction` | `ToolOutputStrippingChatReducerTests` |
| `Agent Creation` | `AgentExtensionsCreateAgentTests`, `ToolSourceValidationTests` |
| `Usage Reporting` | `AgentResponseUsageTests` |
| `Telemetry` | `AgentTelemetryTests` |
| `Agent Run Scope` | `AgentRunScopeTests` |
| `Type Resolution` | `AgentTypeRegistryTests` |
| `Agent Evaluation` | `AgentEvaluationGradingTests`, `AgentEvaluationToolTests`, `AgentEvaluationReportTests` |
| `Integration`, `Local llama.cpp` | `LlamaCppIntegrationTests` |

## Skipped Tests

The four `LlamaCppIntegrationTests` are skipped unless explicitly enabled on a non-CI workstation.

## Local llama.cpp Tests

These tests call a real OpenAI-compatible llama.cpp endpoint. They never run in CI and require an
explicit opt-in plus local environment configuration:

```powershell
$env:CASCAP_RUN_LOCAL_LLAMA_CPP_TESTS = 'true'
$env:CASCAP_LOCAL_LLAMA_CPP_ENDPOINT = 'http://localhost:8080'
$env:CASCAP_LOCAL_LLAMA_CPP_MODEL = '<loaded-model-name>'
$env:CASCAP_LOCAL_LLAMA_CPP_VISION_MODEL = '<loaded-vision-model-name>' # Optional
dotnet test --project src/CasCap.Common.AI.Tests/CasCap.Common.AI.Tests.csproj --filter-trait 'Category=Local llama.cpp'
```

`CASCAP_LOCAL_LLAMA_CPP_API_KEY` defaults to `sk-no-key-required`.
`CASCAP_LOCAL_LLAMA_CPP_TIMEOUT_SECONDS` defaults to 120 seconds. Local endpoints, model names and
credentials remain environment-only. When the endpoint is behind Basic auth, set both
`CASCAP_LOCAL_LLAMA_CPP_BASIC_AUTH_USERNAME` and `CASCAP_LOCAL_LLAMA_CPP_BASIC_AUTH_PASSWORD`.

## File Structure

```text
Tests/
├── AgentEvaluationTestData.cs
├── Integration/
│   ├── LlamaCppIntegrationTests.cs
│   ├── LocalLlamaCppFactAttribute.cs
│   ├── LocalLlamaCppTestEnvironment.cs
│   └── LocalLlamaCppTestSettings.cs
└── Unit/
    ├── AgentEvaluationGradingTests.cs
    ├── AgentEvaluationReportTests.cs
    ├── AgentEvaluationToolTests.cs
    ├── AgentExtensionsCreateAgentTests.cs
    ├── AgentRunScopeTests.cs
    ├── AgentResponseUsageTests.cs
    ├── AgentTelemetryTests.cs
    ├── AgentTypeRegistryTests.cs
    ├── ToolOutputStrippingChatReducerTests.cs
    └── ToolSourceValidationTests.cs
```

## Notes

`ReduceAsync_MixedTextAndToolCall_NoOrphanedToolCall` is a regression test. An assistant
message mixing `TextContent` with `FunctionCallContent` was previously retained while its
matching `FunctionResultContent` message was dropped, leaving an orphaned tool call that
OpenAI and Azure OpenAI reject with HTTP 400.

`AgentResponseUsageTests` pins framework behaviour rather than library behaviour:
`AgentExtensions` previously carried an ambient accumulator because the agent framework did
not surface per-round-trip usage. It now aggregates onto `AgentResponse.Usage`, so these
tests guard the assumption that allowed that accumulator to be deleted.
