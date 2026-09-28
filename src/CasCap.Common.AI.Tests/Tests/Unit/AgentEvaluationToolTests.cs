using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace CasCap.Common.AI.Tests.Unit;

/// <summary>
/// Tests for the evaluation tool surface: catalog classification and filtering, fixture tools, variants,
/// harness tool assembly and the MCP prompt contract.
/// </summary>
[Trait("Category", "Agent Evaluation")]
public class AgentEvaluationToolTests
{
    [McpServerToolType]
    public sealed class OrderMcpQueryService
    {
        [McpServerTool, Description("Gets the status of one order.")]
        public string GetOrderStatus([Description("Order number.")] string orderId) => "live";

        [McpServerTool, Description("Lists every order.")]
        public string[] ListOrders() => ["live"];

        [McpServerTool, Description("Cancels one order.")]
        public bool CancelOrder([Description("Order number.")] string orderId) => true;

        [McpServerTool(ReadOnly = true), Description("Checks an order against the warehouse.")]
        public bool AuditOrder([Description("Order number.")] string orderId) => true;
    }

    [McpServerPromptType]
    public static class OrderMcpPrompts
    {
        [McpServerPrompt, Description("Checks one order.")]
        public static ChatMessage CheckOrder(string orderId = "42") =>
            new(ChatRole.User, $"Use GetOrderStatus for order {orderId}, then GetOrderHistory to explain delays.");
    }

    private static readonly McpToolCatalog Catalog = new([typeof(OrderMcpQueryService)]);

    [Theory]
    [InlineData("get_order_status", ToolDisposition.Query)]
    [InlineData("list_orders", ToolDisposition.Query)]
    [InlineData("cancel_order", ToolDisposition.SideEffect)]
    [InlineData("audit_order", ToolDisposition.Query)]
    public void McpToolCatalog_Classify(string toolName, ToolDisposition expected) =>
        Assert.Equal(expected, Catalog.Classify(GetTool(toolName)));

    [Fact]
    public void McpToolCatalog_GetServiceTools_AppliesFilters()
    {
        var agentConfig = AgentEvaluationTestData.CreateAIConfig(nameof(OrderMcpQueryService)).Agents["OrderAgent"] with
        {
            Tools = [new ToolSource { Service = nameof(OrderMcpQueryService), ExcludeTools = ["cancel_order"] }],
        };

        var tools = Catalog.GetServiceTools(EmptyServiceProvider.Instance, agentConfig);

        Assert.Equal(["audit_order", "get_order_status", "list_orders"], tools.Select(t => t.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task FixtureToolFunction_ReturnsFixtureAndRecords()
    {
        var recorder = new EvaluationRecorder();
        var tool = CreateFixtureTool("get_order_status", recorder, ToolSurfaceVariant.Baseline,
            args => new { Status = "shipped", OrderId = args["orderId"] });

        var result = await tool.InvokeAsync(new AIFunctionArguments { ["orderId"] = "42" }, TestContext.Current.CancellationToken);

        Assert.Equal("shipped", Assert.IsType<JsonElement>(result).GetProperty("status").GetString());
        var call = Assert.Single(recorder.ToolCalls);
        Assert.Equal(ToolDisposition.Query, call.Disposition);
        Assert.True(call.FixtureFound);
        Assert.Contains("42", call.ArgumentsJson);
    }

    [Fact]
    public async Task FixtureToolFunction_SandboxesSideEffects()
    {
        var recorder = new EvaluationRecorder();
        var tool = CreateFixtureTool("cancel_order", recorder, ToolSurfaceVariant.Baseline, _ => true);

        var result = await tool.InvokeAsync(new AIFunctionArguments { ["orderId"] = "42" }, TestContext.Current.CancellationToken);

        Assert.False(Assert.IsType<JsonElement>(result).GetProperty("accepted").GetBoolean());
        Assert.Equal(ToolDisposition.SideEffect, Assert.Single(recorder.ToolCalls).Disposition);
    }

    [Fact]
    public async Task FixtureToolFunction_ReportsMissingFixture()
    {
        var recorder = new EvaluationRecorder();
        var tool = CreateFixtureTool("list_orders", recorder, ToolSurfaceVariant.Baseline, responder: null);

        var result = await tool.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.True(Assert.IsType<JsonElement>(result).TryGetProperty("error", out _));
        Assert.False(Assert.Single(recorder.ToolCalls).FixtureFound);
    }

    [Fact]
    public void FixtureToolFunction_AppliesVariantDescriptions()
    {
        var variant = new ToolSurfaceVariant
        {
            Name = "test",
            DescriptionOverrides = new Dictionary<string, string> { ["get_order_status"] = "Rewritten tool." },
            ParameterDescriptionOverrides = new Dictionary<string, string> { ["get_order_status.orderId"] = "Rewritten parameter." },
        };

        var tool = CreateFixtureTool("get_order_status", new EvaluationRecorder(), variant, responder: null);

        Assert.Equal("Rewritten tool.", tool.Description);
        Assert.Equal("Rewritten parameter.",
            tool.JsonSchema.GetProperty("properties").GetProperty("orderId").GetProperty("description").GetString());
    }

    [Fact]
    public void ToolSurfaceVariant_RewriteSchema_RejectsUnknownParameter()
    {
        var variant = new ToolSurfaceVariant
        {
            Name = "test",
            ParameterDescriptionOverrides = new Dictionary<string, string> { ["get_order_status.missing"] = "x" },
        };

        Assert.Throws<InvalidOperationException>(() => variant.RewriteSchema("get_order_status", GetTool("get_order_status").JsonSchema));
    }

    [Theory]
    [InlineData("Edge", null)]
    [InlineData("AzureNoCredential", "no Entra ID credential or ApiKey configured")]
    [InlineData("OpenAINoKey", "no ApiKey configured")]
    [InlineData("Foundry", "AzureAIFoundry is not supported by CasCap.Common.AI")]
    [InlineData("Missing", "not defined in CasCap:AIConfig:Providers")]
    public void Harness_GetUnavailableReason(string providerKey, string? expected) =>
        Assert.Equal(expected, CreateHarness().GetUnavailableReason(providerKey));

    [Fact]
    public void Harness_GetOfferedTools_WrapsServiceAndDelegationTools()
    {
        var harness = CreateHarness();
        var hidden = new ToolSurfaceVariant { Name = "hidden", HiddenTools = ["list_orders"] };

        var orderTools = harness.GetOfferedTools("OrderAgent", hidden);
        var frontTools = harness.GetOfferedTools("FrontAgent");

        Assert.DoesNotContain(orderTools, t => t.Name == "list_orders");
        Assert.Contains(orderTools, t => t.Name == "cancel_order" && t.Disposition == ToolDisposition.SideEffect);
        var delegation = Assert.Single(frontTools);
        Assert.Equal("invoke_order_agent", delegation.Name);
        Assert.Equal(ToolDisposition.Delegation, delegation.Disposition);
    }

    [Fact]
    public void Harness_GetKnownToolNames_IncludesDelegationAndCandidates()
    {
        var candidate = new ToolSurfaceVariant
        {
            Name = "candidate",
            CandidateTools = new Dictionary<string, AIFunction[]>
            {
                ["OrderAgent"] = [AIFunctionFactory.Create(() => (object?)null, "get_order_eta", "Estimated delivery time.")],
            },
        };

        var known = CreateHarness().GetKnownToolNames([candidate]);

        Assert.Contains("get_order_status", known);
        Assert.Contains("invoke_front_agent", known);
        Assert.Contains("get_order_eta", known);
    }

    [Fact]
    public void McpPromptContract_FindsMissingToolReferences()
    {
        var missing = McpPromptContract.FindMissingToolReferences([typeof(AgentEvaluationToolTests).Assembly]);

        Assert.Contains($"{nameof(OrderMcpPrompts)}.{nameof(OrderMcpPrompts.CheckOrder)} references GetOrderHistory", missing);
        Assert.DoesNotContain(missing, m => m.EndsWith(" references GetOrderStatus", StringComparison.Ordinal));
    }

    private static AgentEvaluationHarness CreateHarness() =>
        new(NullLoggerFactory.Instance, AgentEvaluationTestData.CreateAIConfig(nameof(OrderMcpQueryService)), Catalog,
            typeof(AgentEvaluationToolTests).Assembly);

    private static AIFunction GetTool(string toolName) => Catalog.GetAllTools().Single(t => t.Name == toolName);

    private static FixtureToolFunction CreateFixtureTool(string toolName, EvaluationRecorder recorder, ToolSurfaceVariant variant,
        Func<AIFunctionArguments, object?>? responder)
    {
        var inner = GetTool(toolName);
        return new FixtureToolFunction(inner, "OrderAgent", Catalog.Classify(inner), responder, recorder,
            variant.GetDescription(toolName), variant.RewriteSchema(toolName, inner.JsonSchema));
    }
}
