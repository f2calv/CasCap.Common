namespace CasCap.Common.AI.Tests;

/// <summary>Builders for agent evaluation runs, scenarios and configuration shared by the evaluation tests.</summary>
public static class AgentEvaluationTestData
{
    /// <summary>A scenario that requires one of two query tools and forbids side effects.</summary>
    public static AgentEvaluationScenario OrderStatusScenario { get; } = new()
    {
        Id = "order-status",
        AgentKey = "OrderAgent",
        Question = "What is the status of order 42?",
        Answer = AnswerExpectation.ContainsAny("shipped"),
        RequiredToolGroups = [["get_order_status", "list_orders"]],
    };

    /// <summary>Creates a completed, passing run with a single model request of the given duration.</summary>
    /// <param name="providerKey">The provider key; the model name is derived from it.</param>
    /// <param name="scenarioId">The scenario identifier.</param>
    /// <param name="seconds">The run and request duration.</param>
    public static AgentEvaluationRun CreateRun(string providerKey, string scenarioId, double seconds) => new()
    {
        ScenarioId = scenarioId,
        ProviderKey = providerKey,
        ModelName = $"model-{providerKey}",
        InstructionVariant = InstructionVariant.Baseline.Name,
        ToolSurfaceVariant = ToolSurfaceVariant.Baseline.Name,
        Repetition = 1,
        Answer = "shipped",
        Elapsed = TimeSpan.FromSeconds(seconds),
        AnswerPassed = true,
        ToolSelectionPassed = true,
        RoundTrips = [new ModelRoundTrip("agent", TimeSpan.FromSeconds(seconds), 1, 10, 2, 100, 50, null, 5, null, "stop", 0)],
    };

    /// <summary>
    /// Creates an AI configuration with an order agent, an orchestrator that delegates to it, and one provider
    /// of each supported type.
    /// </summary>
    /// <param name="toolServiceName">The tool type the order agent uses.</param>
    public static AIConfig CreateAIConfig(string toolServiceName) => new()
    {
        Providers = new Dictionary<string, ProviderConfig>
        {
            ["Edge"] = new() { Type = AgentType.OpenAI, Endpoint = new Uri("http://localhost:8080"), ModelName = "edge", ApiKey = "none" },
            ["AzureNoCredential"] = new() { Type = AgentType.AzureOpenAI, Endpoint = new Uri("https://example.com/"), ModelName = "cloud" },
            ["OpenAINoKey"] = new() { Type = AgentType.OpenAI, ModelName = "cloud" },
            ["Foundry"] = new() { Type = AgentType.AzureAIFoundry, ModelName = "cloud" },
        },
        Agents = new Dictionary<string, AgentConfig>
        {
            ["OrderAgent"] = new()
            {
                Provider = "Edge",
                Name = "Order Agent",
                Description = "Answers order questions.",
                Prompt = string.Empty,
                Instructions = "You answer order questions.",
                Tools = [new ToolSource { Service = toolServiceName }],
            },
            ["FrontAgent"] = new()
            {
                Provider = "Edge",
                Name = "Front Agent",
                Description = "Routes questions.",
                Prompt = string.Empty,
                Instructions = "You route questions.",
                Tools = [new ToolSource { Agent = "OrderAgent" }],
            },
        },
    };
}
