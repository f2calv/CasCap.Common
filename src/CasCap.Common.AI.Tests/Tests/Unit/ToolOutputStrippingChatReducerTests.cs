namespace CasCap.Common.AI.Tests.Unit;

/// <summary>
/// Tests for <see cref="ToolOutputStrippingChatReducer"/>, with particular focus on the
/// invariant that no <see cref="FunctionCallContent"/> may survive reduction without its
/// matching <see cref="FunctionResultContent"/> — OpenAI and Azure OpenAI reject such a
/// history with HTTP 400.
/// </summary>
[Trait("Category", "Chat Reduction")]
public class ToolOutputStrippingChatReducerTests
{
    private static ChatMessage System(string text) => new(ChatRole.System, text);

    private static ChatMessage User(string text) => new(ChatRole.User, text);

    private static ChatMessage Assistant(string text) => new(ChatRole.Assistant, text);

    /// <summary>An assistant message carrying only a tool call.</summary>
    private static ChatMessage ToolCall(string callId, string name) =>
        new(ChatRole.Assistant, [new FunctionCallContent(callId, name, new Dictionary<string, object?>())]);

    /// <summary>A tool message carrying only the result of a tool call.</summary>
    private static ChatMessage ToolResult(string callId, string name, object? result) =>
        new(ChatRole.Tool, [new FunctionResultContent(callId, result)]) { AuthorName = name };

    /// <summary>An assistant message mixing narration text with a tool call — the regression trigger.</summary>
    private static ChatMessage MixedTextAndToolCall(string text, string callId, string name) =>
        new(ChatRole.Assistant, [
            new TextContent(text),
            new FunctionCallContent(callId, name, new Dictionary<string, object?>()),
        ]);

    private static async Task<List<ChatMessage>> ReduceAsync(int targetCount, params ChatMessage[] messages) =>
        [.. await new ToolOutputStrippingChatReducer(targetCount).ReduceAsync(messages, TestContext.Current.CancellationToken)];

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_NonPositiveTargetCount(int targetCount)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ToolOutputStrippingChatReducer(targetCount));
        Assert.Equal("targetCount", ex.ParamName);
    }

    [Fact]
    public async Task ReduceAsync_NullMessages() =>
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await new ToolOutputStrippingChatReducer(10).ReduceAsync(null!, TestContext.Current.CancellationToken));

    /// <summary>
    /// Regression test: an assistant message mixing text with a <see cref="FunctionCallContent"/>
    /// was previously retained (because not <i>all</i> of its content was tool content) while the
    /// matching <see cref="FunctionResultContent"/> message was dropped, leaving an orphaned call.
    /// </summary>
    [Fact]
    public async Task ReduceAsync_MixedTextAndToolCall_NoOrphanedToolCall()
    {
        var result = await ReduceAsync(10,
            System("you are a house assistant"),
            User("is the heating on?"),
            MixedTextAndToolCall("Let me check the heat pump.", "call-1", "get_heat_pump_state"),
            ToolResult("call-1", "get_heat_pump_state", new { mode = "heating" }),
            Assistant("Yes, the heating is on."));

        var calls = result.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
        var results = result.SelectMany(m => m.Contents).OfType<FunctionResultContent>().ToList();
        Assert.True(calls.Count == 0, $"expected no orphaned tool calls, found {calls.Count}");
        Assert.True(results.Count == 0, $"expected no tool results, found {results.Count}");
    }

    /// <summary>The narration text of a mixed message survives even though the tool call is stripped.</summary>
    [Fact]
    public async Task ReduceAsync_MixedTextAndToolCall_PreservesText()
    {
        var result = await ReduceAsync(10,
            User("is the heating on?"),
            MixedTextAndToolCall("Let me check the heat pump.", "call-1", "get_heat_pump_state"),
            ToolResult("call-1", "get_heat_pump_state", new { mode = "heating" }));

        var assistant = Assert.Single(result, m => m.Role == ChatRole.Assistant);
        Assert.Equal("Let me check the heat pump.", assistant.Text);
    }

    /// <summary>Messages consisting solely of tool content are removed entirely.</summary>
    [Fact]
    public async Task ReduceAsync_ToolOnlyMessages_Dropped()
    {
        var result = await ReduceAsync(10,
            User("turn on the lights"),
            ToolCall("call-1", "set_light_state"),
            ToolResult("call-1", "set_light_state", true),
            Assistant("Lights are on."));

        Assert.Collection(result,
            m => Assert.Equal(ChatRole.User, m.Role),
            m => Assert.Equal(ChatRole.Assistant, m.Role));
    }

    /// <summary>Stripping must not leave blank message shells behind.</summary>
    [Fact]
    public async Task ReduceAsync_NoEmptyMessagesRetained()
    {
        var result = await ReduceAsync(10,
            System("instructions"),
            User("hello"),
            ToolCall("call-1", "a"),
            ToolResult("call-1", "a", "x"),
            MixedTextAndToolCall("thinking", "call-2", "b"),
            ToolResult("call-2", "b", "y"),
            Assistant("done"));

        Assert.All(result, m => Assert.NotEmpty(m.Contents));
    }

    /// <summary>The agent's system instructions must never be discarded.</summary>
    [Fact]
    public async Task ReduceAsync_PreservesFirstSystemMessage()
    {
        var result = await ReduceAsync(2,
            System("you are a house assistant"),
            User("one"),
            Assistant("two"),
            User("three"),
            Assistant("four"));

        var system = Assert.Single(result, m => m.Role == ChatRole.System);
        Assert.Equal("you are a house assistant", system.Text);
    }

    /// <summary>The sliding window keeps the newest exchanges and discards the oldest.</summary>
    [Theory]
    [InlineData(1, new[] { "four" })]
    [InlineData(2, new[] { "three", "four" })]
    [InlineData(3, new[] { "two", "three", "four" })]
    [InlineData(99, new[] { "one", "two", "three", "four" })]
    public async Task ReduceAsync_SlidingWindow_RetainsMostRecent(int targetCount, string[] expected)
    {
        var result = await ReduceAsync(targetCount,
            System("instructions"),
            User("one"),
            Assistant("two"),
            User("three"),
            Assistant("four"));

        var nonSystem = result.Where(m => m.Role != ChatRole.System).Select(m => m.Text).ToList();
        Assert.Equal(expected, nonSystem);
    }

    /// <summary>A history already within budget and free of tool content is returned untouched.</summary>
    [Fact]
    public async Task ReduceAsync_NothingToReduce()
    {
        var result = await ReduceAsync(10,
            System("instructions"),
            User("hello"),
            Assistant("hi"));

        Assert.Equal(["instructions", "hello", "hi"], result.Select(m => m.Text));
    }

    [Fact]
    public async Task ReduceAsync_EmptyHistory() =>
        Assert.Empty(await ReduceAsync(10));

    /// <summary>Message metadata must survive the content-stripping clone.</summary>
    [Fact]
    public async Task ReduceAsync_PreservesMessageMetadata()
    {
        var message = MixedTextAndToolCall("narration", "call-1", "tool_a");
        message.MessageId = "msg-42";
        message.AuthorName = "comms-agent";

        var result = await ReduceAsync(10, User("hi"), message);

        var assistant = Assert.Single(result, m => m.Role == ChatRole.Assistant);
        Assert.Equal("msg-42", assistant.MessageId);
        Assert.Equal("comms-agent", assistant.AuthorName);
    }

    /// <summary>Stripping must not mutate the caller's message instances.</summary>
    [Fact]
    public async Task ReduceAsync_DoesNotMutateInput()
    {
        var message = MixedTextAndToolCall("narration", "call-1", "tool_a");

        _ = await ReduceAsync(10, User("hi"), message);

        Assert.Equal(2, message.Contents.Count);
        Assert.Single(message.Contents.OfType<FunctionCallContent>());
    }
}
