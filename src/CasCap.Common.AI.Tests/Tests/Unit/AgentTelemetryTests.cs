using System.Diagnostics;

namespace CasCap.Common.AI.Tests.Unit;

/// <summary>
/// Verifies agent-level OpenTelemetry instrumentation. Chat-client-level instrumentation only
/// emits one span per LLM round-trip; the agent-level span covers the whole run, which is what
/// gives a sub-agent fan-out its parent/child structure.
/// </summary>
[Trait("Category", "Telemetry")]
public class AgentTelemetryTests
{
    private sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
    {
        private int _index;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(responses[Math.Min(_index++, responses.Length - 1)]);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    /// <summary>Collects activities emitted by a single source for the duration of a test.</summary>
    private sealed class ActivityCollector : IDisposable
    {
        private readonly ActivityListener _listener;

        public List<Activity> Activities { get; } = [];

        public ActivityCollector(string sourceName)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Activities.Add,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose() => _listener.Dispose();
    }

    private static AIAgent BuildInstrumentedAgent(string sourceName, string agentName, params ChatResponse[] responses) =>
        new ChatClientAgent(
            new ScriptedChatClient(responses),
            new ChatClientAgentOptions { Name = agentName })
            .AsBuilder()
            .UseOpenTelemetry(sourceName, o => o.EnableSensitiveData = false)
            .Build();

    [Fact]
    public async Task UseOpenTelemetry_EmitsAgentSpan()
    {
        const string sourceName = "test.ai.single";
        using var collector = new ActivityCollector(sourceName);
        var agent = BuildInstrumentedAgent(sourceName, "comms-agent",
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));

        await agent.RunAsync("hi", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(collector.Activities);
    }

    /// <summary>
    /// The payload of this change: a sub-agent invoked from within a parent run must produce a
    /// span parented by the parent agent's span, so a fan-out is legible as a trace tree.
    /// </summary>
    [Fact]
    public async Task UseOpenTelemetry_NestsSubAgentSpansUnderParent()
    {
        const string sourceName = "test.ai.nested";
        using var collector = new ActivityCollector(sourceName);

        var subAgent = BuildInstrumentedAgent(sourceName, "security-agent",
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "all clear")));

        var parentChatClient = new ScriptedChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "delegating")));

        var parent = new ChatClientAgent(parentChatClient, new ChatClientAgentOptions { Name = "comms-agent" })
            .AsBuilder()
            .UseOpenTelemetry(sourceName, o => o.EnableSensitiveData = false)
            .Use(async (messages, session, options, innerAgent, ct) =>
            {
                // Stand in for CreateAgentTool's delegation, which runs the sub-agent inside the
                // parent's run and therefore inside the parent's activity scope.
                await subAgent.RunAsync("check the house", cancellationToken: ct);
                return await innerAgent.RunAsync(messages, session, options, ct);
            }, null)
            .Build();

        await parent.RunAsync("is everything ok?", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(collector.Activities.Count >= 2,
            $"expected a parent and a sub-agent span, got {collector.Activities.Count}");

        // The sub-agent span completes first and must carry a parent id.
        var subAgentSpan = collector.Activities[0];
        Assert.False(string.IsNullOrEmpty(subAgentSpan.ParentId),
            "sub-agent span should be parented by the parent agent's span");
    }

    /// <summary>Prompt and response content must stay out of spans unless explicitly opted in.</summary>
    [Fact]
    public async Task UseOpenTelemetry_SensitiveDataOffByDefault()
    {
        const string sourceName = "test.ai.sensitive";
        using var collector = new ActivityCollector(sourceName);

        var agent = new ChatClientAgent(
            new ScriptedChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "the back door is unlocked"))),
            new ChatClientAgentOptions { Name = "comms-agent" })
            .AsBuilder()
            .UseOpenTelemetry(sourceName)
            .Build();

        await agent.RunAsync("is the back door locked?", cancellationToken: TestContext.Current.CancellationToken);

        var allTagValues = collector.Activities
            .SelectMany(a => a.Tags)
            .Select(t => t.Value ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(allTagValues, v => v.Contains("back door", StringComparison.OrdinalIgnoreCase));
    }
}
