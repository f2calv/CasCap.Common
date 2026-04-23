using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CasCap.Models;

/// <summary>
/// Accumulates and exposes the result of an AI agent run, including diagnostic
/// counters captured during streaming or one-shot inference.
/// Supports both streaming accumulation (via <see cref="AppendText"/>) and
/// one-shot construction in <see cref="AgentExtensions.RunAnalysisAsync"/>.
/// </summary>
public sealed class AgentRunResult(string agentName)
{
    private readonly StringBuilder _sb = new();
    private readonly StringBuilder _thinkingSb = new();
    private readonly object _sbLock = new();

    /// <summary>Display name of the agent this result belongs to.</summary>
    public string AgentName { get; } = agentName;

    /// <summary>The provider key used for this agent run.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>The model name used for this agent run.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Nesting depth of this agent run: <c>0</c> for the top-level agent,
    /// <c>1</c> for a sub-agent, <c>2</c> for a sub-sub-agent, and so on.
    /// </summary>
    /// <remarks>Set automatically by the ambient depth counter in <see cref="AgentExtensions"/>.</remarks>
    public int NestingDepth { get; set; }

    /// <summary>
    /// Gets the accumulated/raw output text (thread-safe during streaming).
    /// Built from <see cref="ChatResponseUpdate.Text"/> and <see cref="TextContent.Text"/> items
    /// in <see cref="ChatResponseUpdate.Contents"/>.
    /// </summary>
    public string OutputText { get { lock (_sbLock) return _sb.ToString(); } }

    /// <summary>
    /// The fully formatted result string including model name, endpoint, output and duration.
    /// </summary>
    public string FormattedResult { get; set; } = string.Empty;

    /// <summary>Whether the agent run has finished (successfully or with an error).</summary>
    public bool IsComplete { get; set; }

    /// <summary>Total elapsed time for the agent run (measured locally, not from the model provider).</summary>
    public TimeSpan Elapsed { get; set; }

    /// <summary>
    /// The finish reason returned by the model (e.g. "stop", "length").
    /// Sourced from <see cref="ChatResponseUpdate.FinishReason"/>.
    /// </summary>
    public string FinishReason { get; set; } = string.Empty;

    /// <summary>The captured exception if the agent run failed.</summary>
    public Exception? Error { get; set; }

    /// <summary>The <see cref="AgentSession"/> after the run, if session preservation is enabled.</summary>
    public AgentSession? Session { get; set; }

    // ── Diagnostics / metrics ───────────────────────────────────────────────

    /// <summary>Elapsed time from prompt dispatch to the first text-bearing update.</summary>
    public TimeSpan? TimeToFirstToken { get; set; }

    /// <summary>Total number of response update items received (streaming chunks or 1 for one-shot).</summary>
    public int UpdateCount { get; set; }

    /// <summary>Number of updates that contained text content.</summary>
    public int TextUpdateCount { get; set; }

    /// <summary>Number of updates that contained <see cref="TextReasoningContent"/> thinking content.</summary>
    public int ThinkingUpdateCount { get; set; }

    /// <summary>Number of tool/function calls executed during the agent run.</summary>
    public int ToolCallCount { get; set; }

    /// <summary>Tools invoked during the agent run, in call order (may contain duplicates).</summary>
    public List<ToolCallInfo> ToolCalls { get; } = [];

    /// <summary>Tracks whether the current streaming position is inside <c>&lt;think&gt;</c> tags.</summary>
    public bool IsThinking { get; set; }

    /// <summary>
    /// Response identifier from the model provider.
    /// Sourced from <see cref="ChatResponseUpdate.ResponseId"/>.
    /// </summary>
    public string? ResponseId { get; set; }

    /// <summary>
    /// Timestamp when the response was created by the model provider.
    /// Sourced from <see cref="ChatResponseUpdate.CreatedAt"/>.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Token usage details extracted from <see cref="UsageContent.Details"/> found in
    /// <see cref="ChatResponseUpdate.Contents"/>. Exposes <see cref="UsageDetails.InputTokenCount"/>,
    /// <see cref="UsageDetails.OutputTokenCount"/>, <see cref="UsageDetails.TotalTokenCount"/> and
    /// <see cref="UsageDetails.AdditionalCounts"/>.
    /// </summary>
    public UsageDetails? Usage { get; set; }

    /// <summary>
    /// Additional properties collected from <see cref="ChatResponseUpdate.AdditionalProperties"/>
    /// and <see cref="UsageContent.AdditionalProperties"/>.
    /// </summary>
    public Dictionary<string, object?> AdditionalProperties { get; set; } = [];

    /// <summary>Appends a text fragment to the accumulated output (thread-safe).</summary>
    public void AppendText(string fragment)
    {
        lock (_sbLock)
            _sb.Append(fragment);
    }

    /// <summary>
    /// Gets the accumulated thinking/reasoning text (thread-safe during streaming).
    /// Built from <see cref="TextReasoningContent.Text"/> items in <see cref="ChatResponseUpdate.Contents"/>.
    /// </summary>
    public string ThinkingText { get { lock (_sbLock) return _thinkingSb.ToString(); } }

    /// <summary>Appends a thinking text fragment to the accumulated reasoning output (thread-safe).</summary>
    public void AppendThinkingText(string fragment)
    {
        lock (_sbLock)
            _thinkingSb.Append(fragment);
    }

    /// <summary>Binary attachments (e.g. images) extracted from tool call results during the agent run.</summary>
    public List<AgentRunAttachment> Attachments { get; } = [];
}
