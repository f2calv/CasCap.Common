using CasCap.Extensions;
using Microsoft.Extensions.AI;
using Serilog;

namespace CasCap.Services;

/// <summary>
/// An <see cref="IChatReducer"/> that strips <see cref="FunctionCallContent"/> and
/// <see cref="FunctionResultContent"/> from older messages while retaining the most
/// recent <c>targetCount</c> non-system exchanges.
/// </summary>
/// <remarks>
/// <para>
/// Tool-call and tool-result messages are the primary source of context bloat on
/// edge GPU devices because each tool invocation produces verbose JSON payloads that
/// remain in the chat history indefinitely. This reducer addresses that by:
/// </para>
/// <list type="number">
/// <item><description>Preserving the first system message (if any) so the agent's
/// instructions are never lost.</description></item>
/// <item><description>Dropping all messages that consist solely of
/// <see cref="FunctionCallContent"/> or <see cref="FunctionResultContent"/>.</description></item>
/// <item><description>Keeping at most <c>targetCount</c> of the most recent
/// non-system messages as a sliding window.</description></item>
/// </list>
/// <para>
/// Designed for use with <see cref="Microsoft.Agents.AI.InMemoryChatHistoryProvider"/>
/// via <see cref="Microsoft.Agents.AI.InMemoryChatHistoryProviderOptions.ChatReducer"/>.
/// </para>
/// </remarks>
public sealed class ToolOutputStrippingChatReducer(int targetCount) : IChatReducer
{
    private readonly int _targetCount = targetCount > 0
        ? targetCount
        : throw new ArgumentOutOfRangeException(nameof(targetCount), "Target count must be positive.");

    /// <inheritdoc/>
    public Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var input = messages.ToList();
        var result = GetReducedMessages(input).ToList();

        var toolDropped = input.Count(m => m.Role != ChatRole.System
            && m.Contents.All(c => c is FunctionCallContent or FunctionResultContent));
        var windowDropped = input.Count - toolDropped - (input.Any(m => m.Role == ChatRole.System) ? 1 : 0) - result.Count(m => m.Role != ChatRole.System);

        if (toolDropped > 0 || windowDropped > 0)
        {
            Log.Information("{ClassName} reduced {InputCount} \u2192 {OutputCount} messages (tool-only dropped={ToolDropped}, window trimmed={WindowTrimmed}, target={Target})",
                nameof(ToolOutputStrippingChatReducer), input.Count, result.Count, toolDropped, windowDropped, _targetCount);
            AgentExtensions.GetCompactionCallback()?.Invoke(input.Count, result.Count, toolDropped, windowDropped, _targetCount);
        }

        return Task.FromResult<IEnumerable<ChatMessage>>(result);
    }

    private IEnumerable<ChatMessage> GetReducedMessages(IEnumerable<ChatMessage> messages)
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

            // Drop messages that consist entirely of tool call/result content.
            if (message.Contents.All(c => c is FunctionCallContent or FunctionResultContent))
                continue;

            if (retained.Count >= _targetCount)
                retained.Dequeue();

            retained.Enqueue(message);
        }

        if (systemMessage is not null)
            yield return systemMessage;

        while (retained.Count > 0)
            yield return retained.Dequeue();
    }
}
