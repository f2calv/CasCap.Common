using CasCap.Common.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasCap.Common.Services;

/// <summary>
/// An <see cref="IChatReducer"/> that strips <see cref="FunctionCallContent"/> and
/// <see cref="FunctionResultContent"/> from the chat history while retaining the most
/// recent <c>targetCount</c> non-system exchanges.
/// </summary>
/// <remarks>
/// <para>
/// Tool-call and tool-result messages are the primary source of context bloat on
/// edge GPU devices because each tool invocation produces verbose JSON payloads that
/// remain in the chat history indefinitely. This reducer addresses that by:
/// </para>
/// <list type="number">
/// <item><description>Removing <b>all</b> <see cref="FunctionCallContent"/> and
/// <see cref="FunctionResultContent"/> from non-system messages, dropping any message
/// left with no remaining content.</description></item>
/// <item><description>Preserving the first system message so the agent's instructions
/// are never lost.</description></item>
/// <item><description>Keeping at most <c>targetCount</c> of the most recent non-system
/// messages as a sliding window.</description></item>
/// </list>
/// <para>
/// Tool content is stripped from <b>every</b> message rather than only from messages that
/// consist <i>solely</i> of tool content. An assistant message mixing
/// <see cref="TextContent"/> with <see cref="FunctionCallContent"/> would otherwise be
/// retained while its matching <see cref="FunctionResultContent"/> message was dropped,
/// leaving an orphaned tool call. OpenAI and Azure OpenAI reject a request whose assistant
/// message carries <c>tool_calls</c> without the matching <c>tool</c> messages, so the
/// stripping must be all-or-nothing to keep the history valid across providers.
/// </para>
/// <para>
/// Designed for use with <see cref="Microsoft.Agents.AI.InMemoryChatHistoryProvider"/>
/// via <see cref="Microsoft.Agents.AI.InMemoryChatHistoryProviderOptions.ChatReducer"/>.
/// </para>
/// <para>
/// TODO: replace the hand-rolled sliding window below with the first-party
/// <c>Microsoft.Extensions.AI.MessageCountingChatReducer</c>, whose documented behaviour
/// ("limits non-system messages to a maximum count, preserving the most recent messages
/// and the first system message") matches this type exactly. Blocked on that type leaving
/// experimental status — it is marked <c>[Experimental("MEAI001")]</c> as of
/// Microsoft.Extensions.AI 10.10.0, and this is a packable library so the suppression would
/// surface as churn risk for downstream consumers.
/// See https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.messagecountingchatreducer
/// </para>
/// <para>
/// TODO: also evaluate the richer first-party compaction pipeline
/// (<c>Microsoft.Agents.AI.Compaction.ToolResultCompactionStrategy</c> +
/// <c>SlidingWindowCompactionStrategy</c> composed via <c>PipelineCompactionStrategy</c>),
/// which collapses tool-call groups atomically rather than discarding them and adds
/// <c>CompactionTelemetry</c>. Blocked on the same experimental status — every type in that
/// namespace is marked <c>[MAAI001]</c> as of Microsoft.Agents.AI 1.21.0.
/// See https://github.com/microsoft/agent-framework/tree/main/dotnet/src/Microsoft.Agents.AI/Compaction
/// </para>
/// </remarks>
public sealed class ToolOutputStrippingChatReducer : IChatReducer
{
    private readonly int _targetCount;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="ToolOutputStrippingChatReducer"/> class.</summary>
    /// <param name="targetCount">The maximum number of non-system messages to retain.</param>
    /// <param name="loggerFactory">Optional logger factory; when omitted the reducer logs nothing.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="targetCount"/> is zero or negative.</exception>
    public ToolOutputStrippingChatReducer(int targetCount, ILoggerFactory? loggerFactory = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetCount);
        _targetCount = targetCount;
        _logger = loggerFactory?.CreateLogger<ToolOutputStrippingChatReducer>()
            ?? (ILogger)NullLogger<ToolOutputStrippingChatReducer>.Instance;
    }

    /// <inheritdoc/>
    public Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var input = messages.ToList();

        var stripped = StripToolContent(input);
        var toolDropped = input.Count - stripped.Count;

        var result = ApplySlidingWindow(stripped);
        var windowTrimmed = stripped.Count - result.Count;

        if (toolDropped > 0 || windowTrimmed > 0)
        {
            _logger.LogDebug("Reduced {InputCount} \u2192 {OutputCount} messages (tool-only dropped={ToolDropped}, window trimmed={WindowTrimmed}, target={Target})",
                input.Count, result.Count, toolDropped, windowTrimmed, _targetCount);
            AgentExtensions.GetCompactionCallback()?.Invoke(input.Count, result.Count, toolDropped, windowTrimmed, _targetCount);
        }

        return Task.FromResult<IEnumerable<ChatMessage>>(result);
    }

    /// <summary>
    /// Removes every <see cref="FunctionCallContent"/> and <see cref="FunctionResultContent"/>
    /// item from non-system messages, dropping any message left with no content.
    /// </summary>
    /// <remarks>
    /// Stripping partially (i.e. only whole tool-only messages) would leave an assistant
    /// message carrying an orphaned <see cref="FunctionCallContent"/> whose matching
    /// <see cref="FunctionResultContent"/> had been removed, which OpenAI and Azure OpenAI
    /// reject with HTTP 400.
    /// </remarks>
    private static List<ChatMessage> StripToolContent(List<ChatMessage> messages)
    {
        var result = new List<ChatMessage>(messages.Count);

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System
                || !message.Contents.Any(c => c is FunctionCallContent or FunctionResultContent))
            {
                result.Add(message);
                continue;
            }

            var retained = message.Contents
                .Where(c => c is not (FunctionCallContent or FunctionResultContent))
                .ToList();

            // Message carried nothing but tool content — drop it entirely.
            if (retained.Count == 0)
                continue;

            // Clone rather than mutate so the caller's message instances are left intact.
            var clone = message.Clone();
            clone.Contents = retained;
            result.Add(clone);
        }

        return result;
    }

    /// <summary>
    /// Retains the first system message plus the most recent <see cref="_targetCount"/>
    /// non-system messages, preserving the original ordering.
    /// </summary>
    private List<ChatMessage> ApplySlidingWindow(List<ChatMessage> messages)
    {
        ChatMessage? systemMessage = null;
        var retained = new Queue<ChatMessage>(capacity: _targetCount);

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                systemMessage ??= message;
                continue;
            }

            if (retained.Count >= _targetCount)
                retained.Dequeue();

            retained.Enqueue(message);
        }

        var result = new List<ChatMessage>(retained.Count + 1);
        if (systemMessage is not null)
            result.Add(systemMessage);
        result.AddRange(retained);

        return result;
    }
}
