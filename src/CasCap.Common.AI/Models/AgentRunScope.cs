namespace CasCap.Common.Models;

/// <summary>
/// Per-run state and host callbacks for an agent invocation, replacing the ambient
/// <see cref="System.Threading.AsyncLocal{T}"/> fields that <c>AgentExtensions</c> previously
/// required callers to set and clear in matching pairs.
/// </summary>
/// <remarks>
/// <para>
/// A scope is created by the host, passed to <c>RunAnalysisAsync</c>, and shared with every
/// sub-agent delegation beneath that run. Child scopes created by <see cref="ForSubAgent"/>
/// carry an incremented <see cref="Depth"/> but share the parent's callbacks and attachment
/// collection, so attachments produced deep in a fan-out still surface on the top-level result.
/// </para>
/// <para>
/// TODO: the scope is still propagated to sub-agent tool invocations through a single internal
/// <c>AsyncLocal</c>, because the <c>AIFunction</c> delegate signature offers no parameter channel
/// for it. Investigate flowing it explicitly via <c>AIFunctionArguments.Context</c> /
/// <c>FunctionInvocationContext</c> instead, which would remove the last ambient carrier.
/// See https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aifunctionarguments
/// </para>
/// </remarks>
public sealed class AgentRunScope
{
    private readonly List<AgentRunAttachment> _attachments;
    private readonly Lock _attachmentsLock;

    /// <summary>Initializes a new top-level scope.</summary>
    public AgentRunScope()
    {
        _attachments = [];
        _attachmentsLock = new Lock();
    }

    private AgentRunScope(AgentRunScope parent, int depth)
    {
        _attachments = parent._attachments;
        _attachmentsLock = parent._attachmentsLock;
        OnDelegation = parent.OnDelegation;
        OnCompletion = parent.OnCompletion;
        OnCompaction = parent.OnCompaction;
        Depth = depth;
    }

    /// <summary>
    /// Nesting depth of the current run: <c>0</c> for the top-level agent, <c>1</c> for a
    /// sub-agent, and so on.
    /// </summary>
    public int Depth { get; private init; }

    /// <summary>
    /// Invoked when a sub-agent delegation begins, with the agent key, nesting depth and the
    /// target agent's provider. Use for live progress notifications.
    /// </summary>
    public Func<string, int, ProviderConfig, CancellationToken, Task>? OnDelegation { get; init; }

    /// <summary>
    /// Invoked when a sub-agent delegation completes, with the agent key, nesting depth and result.
    /// </summary>
    public Func<string, int, AgentRunResult, CancellationToken, Task>? OnCompletion { get; init; }

    /// <summary>Invoked when the chat history is compacted during this run.</summary>
    public Action<CompactionStats>? OnCompaction { get; init; }

    /// <summary>Attachments accumulated during this run, including those bubbled up from sub-agents.</summary>
    public IReadOnlyList<AgentRunAttachment> Attachments
    {
        get { lock (_attachmentsLock) return [.. _attachments]; }
    }

    /// <summary>Adds an attachment produced by a tool or sub-agent during this run.</summary>
    /// <param name="attachment">The attachment to record.</param>
    public void AddAttachment(AgentRunAttachment attachment)
    {
        lock (_attachmentsLock)
            _attachments.Add(attachment);
    }

    /// <summary>Removes and returns every attachment accumulated so far.</summary>
    public List<AgentRunAttachment> DrainAttachments()
    {
        lock (_attachmentsLock)
        {
            var drained = new List<AgentRunAttachment>(_attachments);
            _attachments.Clear();
            return drained;
        }
    }

    /// <summary>Creates a child scope for a sub-agent delegation, sharing callbacks and attachments.</summary>
    public AgentRunScope ForSubAgent() => new(this, Depth + 1);
}

/// <summary>Summary of a single chat-history compaction pass.</summary>
/// <param name="InputCount">Total messages before compaction.</param>
/// <param name="OutputCount">Total messages after compaction.</param>
/// <param name="ToolDropped">Messages dropped because they consisted solely of tool content.</param>
/// <param name="WindowTrimmed">Messages dropped by the sliding window to meet <paramref name="Target"/>.</param>
/// <param name="Target">The configured maximum non-system message count.</param>
public readonly record struct CompactionStats(
    int InputCount,
    int OutputCount,
    int ToolDropped,
    int WindowTrimmed,
    int Target);
