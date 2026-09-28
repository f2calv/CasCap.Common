namespace CasCap.Common.Models;

/// <summary>
/// One request to the model and its response — a single step of an agent's tool-calling loop.
/// </summary>
/// <param name="AgentKey">The agent whose chat client made the request.</param>
/// <param name="Elapsed">Wall-clock duration of the request, including prompt processing and generation.</param>
/// <param name="MessageCount">Messages sent, including history and tool results.</param>
/// <param name="InstructionCharacters">Characters of system instructions sent with the request.</param>
/// <param name="ToolCount">Tool definitions sent with the request.</param>
/// <param name="ToolSchemaCharacters">Characters of tool names, descriptions and schemas sent with the request.</param>
/// <param name="InputTokens">Prompt tokens reported by the provider.</param>
/// <param name="CachedInputTokens">Prompt tokens served from the provider's prompt cache, when reported.</param>
/// <param name="OutputTokens">Generated tokens reported by the provider.</param>
/// <param name="ReasoningTokens">Reasoning tokens reported by the provider, when reported.</param>
/// <param name="FinishReason">The provider's finish reason, for example <c>tool_calls</c> or <c>stop</c>.</param>
/// <param name="ReasoningCharacters">
/// Characters of visible thinking in the response — reasoning content or <c>&lt;think&gt;</c> blocks. Non-zero on an edge
/// model means thinking is switched on, which multiplies latency.
/// </param>
public sealed record ModelRoundTrip(
    string AgentKey,
    TimeSpan Elapsed,
    int MessageCount,
    int InstructionCharacters,
    int ToolCount,
    int ToolSchemaCharacters,
    long? InputTokens,
    long? CachedInputTokens,
    long? OutputTokens,
    long? ReasoningTokens,
    string? FinishReason,
    int ReasoningCharacters);
