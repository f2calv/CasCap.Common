using System.Runtime.CompilerServices;

namespace CasCap.Common.Services;

/// <summary>
/// Records the duration, context size, token usage and visible thinking of every model request an agent makes.
/// </summary>
/// <remarks>
/// Inserted after function invocation in the chat client pipeline, so each step of a tool-calling loop is
/// recorded separately. This exposes where time goes when an agent delegates to a sub-agent.
/// </remarks>
/// <param name="innerClient">The provider chat client.</param>
/// <param name="agentKey">The agent the client belongs to.</param>
/// <param name="recorder">The run's recorder.</param>
public sealed partial class RecordingChatClient(IChatClient innerClient, string agentKey, EvaluationRecorder recorder)
    : DelegatingChatClient(innerClient)
{
    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var messageList = messages as IReadOnlyCollection<ChatMessage> ?? [.. messages];
        var stopwatch = Stopwatch.StartNew();
        var response = await base.GetResponseAsync(messageList, options, cancellationToken).ConfigureAwait(false);
        Record(stopwatch.Elapsed, messageList.Count, options, response.Usage, response.FinishReason,
            CountReasoningCharacters(response.Messages.SelectMany(m => m.Contents)));
        return response;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messageList = messages as IReadOnlyCollection<ChatMessage> ?? [.. messages];
        var stopwatch = Stopwatch.StartNew();
        UsageDetails? usage = null;
        ChatFinishReason? finishReason = null;
        var contents = new List<AIContent>();
        await foreach (var update in base.GetStreamingResponseAsync(messageList, options, cancellationToken).ConfigureAwait(false))
        {
            foreach (var usageContent in update.Contents.OfType<UsageContent>())
                usage = usageContent.Details;
            contents.AddRange(update.Contents);
            finishReason = update.FinishReason ?? finishReason;
            yield return update;
        }

        Record(stopwatch.Elapsed, messageList.Count, options, usage, finishReason, CountReasoningCharacters(contents));
    }

    /// <summary>
    /// Counts characters of reasoning content and of <c>&lt;think&gt;</c> blocks left inside answer text.
    /// </summary>
    /// <param name="contents">The response contents.</param>
    public static int CountReasoningCharacters(IEnumerable<AIContent> contents) =>
        contents.Sum(c => c switch
        {
            TextReasoningContent reasoning => reasoning.Text.Length,
            TextContent text => ThinkBlockRegex().Matches(text.Text).Sum(m => m.Groups[1].Value.Trim().Length),
            _ => 0,
        });

    private void Record(TimeSpan elapsed, int messageCount, ChatOptions? options, UsageDetails? usage,
        ChatFinishReason? finishReason, int reasoningCharacters) =>
        recorder.Add(new ModelRoundTrip(
            agentKey,
            elapsed,
            messageCount,
            options?.Instructions?.Length ?? 0,
            options?.Tools?.Count ?? 0,
            ToolFootprint.Characters(options?.Tools),
            usage?.InputTokenCount,
            usage?.CachedInputTokenCount,
            usage?.OutputTokenCount,
            usage?.ReasoningTokenCount,
            finishReason?.Value,
            reasoningCharacters));

    [GeneratedRegex(@"<think>(.*?)(?:</think>|$)", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ThinkBlockRegex();
}
