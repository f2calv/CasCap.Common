using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace CasCap.Common.AI.Tests.Unit;

/// <summary>
/// Verifies that tool exceptions and MCP <c>isError</c> results are flagged on <see cref="AgentRunResult.ToolCalls"/>
/// instead of passing to the model as ordinary output.
/// </summary>
[Trait("Category", "Tool Failures")]
public sealed class ToolFailureTests
{
    [Fact]
    public async Task RunAnalysisAsync_FlagsThrowingAndErrorResultTools()
    {
        var agent = BuildAgent(
            [
                AIFunctionFactory.Create(() => "ok", "healthy_tool"),
                AIFunctionFactory.Create(string () => throw new InvalidOperationException("boom"), "throwing_tool"),
                AIFunctionFactory.Create(() => JsonSerializer.SerializeToElement(new { isError = true }), "error_result_tool"),
            ],
            new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent("call-1", "healthy_tool", new Dictionary<string, object?>()),
                new FunctionCallContent("call-2", "throwing_tool", new Dictionary<string, object?>()),
                new FunctionCallContent("call-3", "error_result_tool", new Dictionary<string, object?>()),
            ])),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));

        var result = await agent.RunAnalysisAsync(
            new ProviderConfig { Type = AgentType.Ollama, ModelName = "test-model", Endpoint = new Uri("http://ollama.local:11434") },
            new AgentConfig { Provider = "test-provider", Name = "failure-probe", Description = "d", Prompt = "p", Instructions = "i" },
            AgentExtensions.BuildChatMessage("hi"),
            new ChatOptions(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [("healthy_tool", false), ("throwing_tool", true), ("error_result_tool", true)],
            result.ToolCalls.Select(toolCall => (toolCall.Name, toolCall.Failed)));
        Assert.Equal(2, result.ToolErrorCount);
    }

    [Theory]
    [InlineData("{\"isError\":true,\"content\":[]}", true)]
    [InlineData("{\"isError\":false,\"content\":[]}", false)]
    [InlineData("{\"content\":[]}", false)]
    [InlineData("[]", false)]
    public void IsErrorToolResult_ReadsSerializedCallToolResults(string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, AgentExtensions.IsErrorToolResult(document.RootElement.Clone()));
    }

    [Fact]
    public void IsErrorToolResult_ReadsCallToolResultsAndIgnoresText()
    {
        Assert.True(AgentExtensions.IsErrorToolResult(new CallToolResult { IsError = true, Content = [] }));
        Assert.False(AgentExtensions.IsErrorToolResult("isError"));
        Assert.False(AgentExtensions.IsErrorToolResult(null));
    }

    private static AIAgent BuildAgent(IList<AITool> tools, params ChatResponse[] responses)
    {
        var chatClient = new ScriptedChatClient(responses).AsBuilder().UseFunctionInvocation().Build();
        return new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "failure-probe",
            ChatOptions = new ChatOptions { Tools = tools },
        })
            .AsBuilder()
            .Use(AgentExtensions.CreateFunctionCallingMiddleware())
            .Build();
    }

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
}
