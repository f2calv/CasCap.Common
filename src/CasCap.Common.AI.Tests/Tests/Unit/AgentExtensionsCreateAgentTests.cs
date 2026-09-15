namespace CasCap.Common.AI.Tests.Unit;

/// <summary>
/// Tests for provider validation in <see cref="AgentExtensions.CreateAgent"/>, ensuring a
/// misconfigured <see cref="ProviderConfig"/> fails fast with a message naming the agent and
/// the missing setting rather than surfacing deep inside an SDK.
/// </summary>
[Trait("Category", "Agent Creation")]
public class AgentExtensionsCreateAgentTests
{
    private static AgentConfig NewAgentConfig(string name = "test-agent") => new()
    {
        Provider = "test-provider",
        Name = name,
        Description = "test agent",
        Prompt = "describe the event",
        Instructions = "you are a test agent",
    };

    private static ProviderConfig NewProviderConfig(AgentType type, Uri? endpoint = null, string? apiKey = null) => new()
    {
        Type = type,
        ModelName = "test-model",
        Endpoint = endpoint,
        ApiKey = apiKey,
    };

    [Fact]
    public void CreateAgent_OllamaWithoutEndpoint()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AgentExtensions.CreateAgent(NewProviderConfig(AgentType.Ollama), NewAgentConfig()));

        Assert.Contains("test-agent", ex.Message);
        Assert.Contains(nameof(ProviderConfig.Endpoint), ex.Message);
        Assert.Contains(nameof(AgentType.Ollama), ex.Message);
    }

    /// <summary>An explicitly supplied <see cref="HttpClient"/> may carry the endpoint instead.</summary>
    [Fact]
    public void CreateAgent_OllamaWithEndpointOnHttpClient()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://ollama.local:11434") };

        var (chatClient, agent, instructions) = AgentExtensions.CreateAgent(
            NewProviderConfig(AgentType.Ollama), NewAgentConfig(), httpClient);

        Assert.NotNull(chatClient);
        Assert.NotNull(agent);
        Assert.Equal("you are a test agent", instructions);
    }

    [Fact]
    public void CreateAgent_OllamaWithEndpoint()
    {
        var (chatClient, agent, _) = AgentExtensions.CreateAgent(
            NewProviderConfig(AgentType.Ollama, new Uri("http://ollama.local:11434")), NewAgentConfig());

        Assert.NotNull(chatClient);
        Assert.NotNull(agent);
    }

    [Fact]
    public void CreateAgent_AzureOpenAIWithoutEndpoint()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AgentExtensions.CreateAgent(NewProviderConfig(AgentType.AzureOpenAI), NewAgentConfig()));

        Assert.Contains("test-agent", ex.Message);
        Assert.Contains(nameof(ProviderConfig.Endpoint), ex.Message);
    }

    /// <summary>Azure OpenAI requires either a token credential or an API key.</summary>
    [Fact]
    public void CreateAgent_AzureOpenAIWithoutCredential()
    {
        var provider = NewProviderConfig(AgentType.AzureOpenAI, new Uri("https://contoso.openai.azure.com/"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AgentExtensions.CreateAgent(provider, NewAgentConfig()));

        Assert.Contains("test-agent", ex.Message);
        Assert.Contains(nameof(ProviderConfig.ApiKey), ex.Message);
    }

    [Fact]
    public void CreateAgent_OpenAIWithoutApiKey()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AgentExtensions.CreateAgent(NewProviderConfig(AgentType.OpenAI), NewAgentConfig()));

        Assert.Contains("test-agent", ex.Message);
        Assert.Contains(nameof(ProviderConfig.ApiKey), ex.Message);
    }

    /// <summary>An OpenAI endpoint is optional — the SDK falls back to its own default.</summary>
    [Fact]
    public void CreateAgent_OpenAIWithoutEndpoint()
    {
        var (chatClient, agent, _) = AgentExtensions.CreateAgent(
            NewProviderConfig(AgentType.OpenAI, apiKey: "sk-test"), NewAgentConfig());

        Assert.NotNull(chatClient);
        Assert.NotNull(agent);
    }

    [Theory]
    [InlineData(AgentType.AzureAIFoundry)]
    [InlineData((AgentType)999)]
    public void CreateAgent_UnsupportedProviderType(AgentType type)
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            AgentExtensions.CreateAgent(NewProviderConfig(type), NewAgentConfig()));

        Assert.Contains(type.ToString(), ex.Message);
    }

    /// <summary>
    /// Numeric values are pinned because configuration may bind <see cref="ProviderConfig.Type"/>
    /// from an integer; removing a member must not silently remap the remaining ones.
    /// </summary>
    [Theory]
    [InlineData(AgentType.AzureOpenAI, 1)]
    [InlineData(AgentType.AzureAIFoundry, 2)]
    [InlineData(AgentType.Ollama, 3)]
    [InlineData(AgentType.OpenAI, 4)]
    public void AgentType_NumericValuesAreStable(AgentType type, int expected) =>
        Assert.Equal(expected, (int)type);
}
