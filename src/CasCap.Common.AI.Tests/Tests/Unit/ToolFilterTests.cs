namespace CasCap.Tests.Unit;

/// <summary>Tests that include and exclude filters never silently shrink an agent's tool or prompt set.</summary>
[Trait("Category", "Agent Creation")]
public sealed class ToolFilterTests
{
    private static readonly AITool[] Tools =
    [
        AIFunctionFactory.Create(() => "ok", "get_status"),
        AIFunctionFactory.Create(() => "ok", "get_groups"),
    ];

    [Fact]
    public void FilterTools_IncludedToolsAreReturned()
    {
        var filtered = AgentExtensions.FilterTools(Tools, new ToolSource { IncludeTools = ["get_status"] });

        Assert.Equal(["get_status"], filtered.Select(tool => tool.Name));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FilterTools_UnknownNameThrows(bool include)
    {
        var source = include
            ? new ToolSource { IncludeTools = ["get_status", "send_message"] }
            : new ToolSource { ExcludeTools = ["send_message"] };

        var exception = Assert.Throws<InvalidOperationException>(() => AgentExtensions.FilterTools(Tools, source).ToList());

        Assert.Contains("send_message", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FilterPrompts_UnknownNameThrows()
    {
        var prompts = new[] { new McpPromptDescriptor { Name = "summary" } };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentExtensions.FilterPrompts(prompts, new PromptSource { IncludePrompts = ["missing"] }).ToList());

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }
}
