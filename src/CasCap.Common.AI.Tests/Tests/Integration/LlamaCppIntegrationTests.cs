namespace CasCap.Common.AI.Tests.Integration;

/// <summary>Exercises real OpenAI-compatible llama.cpp inference on an explicitly configured local endpoint.</summary>
[Trait("Category", "Integration")]
[Trait("Category", "Local llama.cpp")]
public sealed class LlamaCppIntegrationTests
{
    private sealed class BasicAuthenticationHandler(string parameter) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", parameter);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private const string SyntheticPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z4QAAAABJRU5ErkJggg==";

    [LocalLlamaCppFact]
    public async Task GetResponseAsync_TextPromptReturnsResponse()
    {
        var settings = GetSettings();
        var (chatClient, _, _) = CreateAgent(settings, settings.ModelName);
        using (chatClient)
        using (var cancellationTokenSource = CreateTimeout(settings))
        {
            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Reply with exactly the word pong.")],
                cancellationToken: cancellationTokenSource.Token);

            Assert.False(string.IsNullOrWhiteSpace(response.Text));
        }
    }

    [LocalLlamaCppFact]
    public async Task GetStreamingResponseAsync_TextPromptYieldsText()
    {
        var settings = GetSettings();
        var (chatClient, _, _) = CreateAgent(settings, settings.ModelName);
        using (chatClient)
        using (var cancellationTokenSource = CreateTimeout(settings))
        {
            var output = new StringBuilder();
            await foreach (var update in chatClient.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Reply with exactly the word stream.")],
                cancellationToken: cancellationTokenSource.Token))
            {
                output.Append(update.Text);
            }

            Assert.False(string.IsNullOrWhiteSpace(output.ToString()));
        }
    }

    [LocalLlamaCppFact]
    public async Task GetResponseAsync_ImageReturnsResponse()
    {
        var settings = GetSettings();
        Assert.SkipUnless(
            !string.IsNullOrWhiteSpace(settings.VisionModelName),
            "Set CASCAP_LOCAL_LLAMA_CPP_VISION_MODEL to run the multimodal test.");
        var (chatClient, _, _) = CreateAgent(settings, settings.VisionModelName!);
        using (chatClient)
        using (var cancellationTokenSource = CreateTimeout(settings))
        {
            var message = new ChatMessage(ChatRole.User,
            [
                new TextContent("Describe this image briefly."),
                new DataContent(Convert.FromBase64String(SyntheticPngBase64), "image/png"),
            ]);
            var response = await chatClient.GetResponseAsync(
                [message],
                cancellationToken: cancellationTokenSource.Token);

            Assert.False(string.IsNullOrWhiteSpace(response.Text));
        }
    }

    [LocalLlamaCppFact]
    public async Task RunAnalysisAsync_SessionMaintainsContext()
    {
        var settings = GetSettings();
        var (chatClient, agent, instructions) = CreateAgent(settings, settings.ModelName);
        using (chatClient)
        using (var cancellationTokenSource = CreateTimeout(settings))
        {
            var agentConfig = CreateAgentConfig();
            var provider = CreateProvider(settings, settings.ModelName);
            var chatOptions = AgentExtensions.BuildChatOptions(agentConfig, instructions);
            var first = await agent.RunAnalysisAsync(
                provider,
                agentConfig,
                AgentExtensions.BuildChatMessage("Remember the marker violet-7319. Confirm briefly."),
                chatOptions,
                cancellationToken: cancellationTokenSource.Token);
            var second = await agent.RunAnalysisAsync(
                provider,
                agentConfig,
                AgentExtensions.BuildChatMessage("What marker did I ask you to remember?"),
                chatOptions,
                session: first.Session,
                cancellationToken: cancellationTokenSource.Token);

            Assert.Contains("violet-7319", second.OutputText, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static LocalLlamaCppTestSettings GetSettings()
    {
        var configured = LocalLlamaCppTestSettings.TryLoad(out var settings);
        Assert.SkipUnless(
            configured,
            "Set CASCAP_LOCAL_LLAMA_CPP_ENDPOINT and CASCAP_LOCAL_LLAMA_CPP_MODEL to run local llama.cpp tests.");
        return settings!;
    }

    private static (IChatClient ChatClient, AIAgent Agent, string Instructions) CreateAgent(
        LocalLlamaCppTestSettings settings,
        string modelName)
    {
        HttpMessageHandler handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(settings.BasicAuthUsername))
        {
            var parameter = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{settings.BasicAuthUsername}:{settings.BasicAuthPassword}"));
            handler = new BasicAuthenticationHandler(parameter) { InnerHandler = handler };
        }

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = settings.Endpoint,
            Timeout = Timeout.InfiniteTimeSpan,
        };

        return AgentExtensions.CreateAgent(
            CreateProvider(settings, modelName),
            CreateAgentConfig(),
            httpClient);
    }

    private static ProviderConfig CreateProvider(LocalLlamaCppTestSettings settings, string modelName) => new()
    {
        Type = AgentType.OpenAI,
        Endpoint = settings.Endpoint,
        ModelName = modelName,
        ApiKey = Environment.GetEnvironmentVariable("CASCAP_LOCAL_LLAMA_CPP_API_KEY") ?? "sk-no-key-required",
    };

    private static AgentConfig CreateAgentConfig() => new()
    {
        Provider = "local-llama-cpp",
        Name = "local-llama-cpp-test",
        Description = "Local llama.cpp integration test agent.",
        Prompt = "Respond to the test request.",
        Instructions = "Follow the user's request precisely and answer concisely.",
    };

    private static CancellationTokenSource CreateTimeout(LocalLlamaCppTestSettings settings)
    {
        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellationTokenSource.CancelAfter(settings.Timeout);
        return cancellationTokenSource;
    }
}