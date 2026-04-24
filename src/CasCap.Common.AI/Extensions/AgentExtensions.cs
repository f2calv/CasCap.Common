using Azure.AI.OpenAI;
using Azure.Core;
using OllamaSharp;
using OpenAI;
using System.ClientModel;
using System.Text.Json;

namespace CasCap.Extensions;

/// <summary>
/// Extension methods for <see cref="AIAgent"/> that simplify creating agents and running inference
/// with text and optional binary content (e.g. images).
/// </summary>
public static partial class AgentExtensions
{
    /// <summary>
    /// Ambient attachment accumulator — sub-agent tool invocations append image attachments
    /// here so the parent <see cref="RunAnalysisAsync"/> can collect them into the final
    /// <see cref="AgentRunResult.Attachments"/>.
    /// </summary>
    private static readonly AsyncLocal<List<AgentRunAttachment>?> _ambientAttachments = new();

    /// <summary>
    /// Ambient nesting depth counter — incremented each time <see cref="CreateAgentTool"/>
    /// delegates to a sub-agent so that <see cref="AgentRunResult.NestingDepth"/> reflects
    /// the current delegation level (<c>0</c> = top-level, <c>1</c> = sub-agent, etc.).
    /// </summary>
    private static readonly AsyncLocal<int> _ambientDepth = new();

    /// <summary>
    /// Ambient binary content — set by the host before running the parent agent so that
    /// sub-agent tool invocations can forward attachments (e.g. audio bytes) via the
    /// <c>forwardAttachment</c> parameter on <see cref="CreateAgentTool"/>.
    /// </summary>
    private static readonly AsyncLocal<(byte[] Bytes, string MimeType)?> _ambientBinaryContent = new();

    /// <summary>
    /// Accumulated <see cref="UsageDetails"/> across all <see cref="IChatClient.GetResponseAsync"/>
    /// round-trips within a single <see cref="RunAnalysisAsync"/> invocation. The middleware
    /// (<see cref="ChatResponseMiddleware"/>) aggregates usage here because
    /// <c>ChatClientAgent.RunAsync</c> only surfaces messages — the per-call
    /// <see cref="ChatResponse.Usage"/> is otherwise lost.
    /// </summary>
    private static readonly AsyncLocal<UsageDetails?> _accumulatedUsage = new();

    /// <summary>
    /// Ambient callback invoked when a sub-agent delegation begins. The parameters are
    /// the agent key, the nesting depth, and a cancellation token.
    /// Set by the host (e.g. <c>CommunicationsBgService</c>) before calling
    /// <see cref="RunAnalysisAsync"/> and cleared afterwards.
    /// </summary>
    private static readonly AsyncLocal<Func<string, int, ProviderConfig, CancellationToken, Task>?> _delegationCallback = new();

    /// <summary>Sets the ambient delegation callback for the current async flow.</summary>
    /// <param name="callback">The callback to invoke on each sub-agent delegation, or <see langword="null"/> to clear.</param>
    public static void SetDelegationCallback(Func<string, int, ProviderConfig, CancellationToken, Task>? callback) =>
        _delegationCallback.Value = callback;

    /// <summary>Clears the ambient delegation callback for the current async flow.</summary>
    public static void ClearDelegationCallback() => _delegationCallback.Value = null;

    /// <summary>
    /// Ambient callback invoked when a sub-agent delegation completes. The parameters are
    /// the agent key, the nesting depth, the <see cref="AgentRunResult"/>, and a cancellation token.
    /// Set by the host (e.g. <c>CommunicationsBgService</c>) before calling
    /// <see cref="RunAnalysisAsync"/> and cleared afterwards.
    /// </summary>
    private static readonly AsyncLocal<Func<string, int, AgentRunResult, CancellationToken, Task>?> _completionCallback = new();

    /// <summary>Sets the ambient completion callback for the current async flow.</summary>
    /// <param name="callback">The callback to invoke when each sub-agent delegation completes, or <see langword="null"/> to clear.</param>
    public static void SetCompletionCallback(Func<string, int, AgentRunResult, CancellationToken, Task>? callback) =>
        _completionCallback.Value = callback;

    /// <summary>Clears the ambient completion callback for the current async flow.</summary>
    public static void ClearCompletionCallback() => _completionCallback.Value = null;

    /// <summary>
    /// Ambient callback invoked when the <see cref="CasCap.Services.ToolOutputStrippingChatReducer"/>
    /// compacts the chat history. Parameters: input count, output count, tool-only dropped, window trimmed, target count.
    /// Set by the host (e.g. <c>CommunicationsBgService</c>) before calling
    /// <see cref="RunAnalysisAsync"/> and cleared afterwards.
    /// </summary>
    private static readonly AsyncLocal<Action<int, int, int, int, int>?> _compactionCallback = new();

    /// <summary>Sets the ambient compaction callback for the current async flow.</summary>
    /// <param name="callback">The callback to invoke when chat history compaction occurs, or <see langword="null"/> to clear.</param>
    public static void SetCompactionCallback(Action<int, int, int, int, int>? callback) =>
        _compactionCallback.Value = callback;

    /// <summary>Clears the ambient compaction callback for the current async flow.</summary>
    public static void ClearCompactionCallback() => _compactionCallback.Value = null;

    /// <summary>Gets the ambient compaction callback for the current async flow.</summary>
    internal static Action<int, int, int, int, int>? GetCompactionCallback() => _compactionCallback.Value;

    /// <summary>Sets the ambient binary content so sub-agent delegations can forward attachments.</summary>
    /// <param name="bytes">The binary payload (e.g. audio bytes).</param>
    /// <param name="mimeType">The MIME type of the binary content (e.g. <c>"audio/aac"</c>).</param>
    public static void SetAmbientBinaryContent(byte[] bytes, string mimeType) =>
        _ambientBinaryContent.Value = (bytes, mimeType);

    /// <summary>Clears the ambient binary content for the current async flow.</summary>
    public static void ClearAmbientBinaryContent() => _ambientBinaryContent.Value = null;

    /// <summary>
    /// Ambient audio debug artifacts — captures the original audio bytes, MIME type, and optional
    /// transcoded WAV so that the debug Signal message can include both files as data-URI attachments.
    /// </summary>
    private static readonly AsyncLocal<AudioDebugArtifacts?> _ambientAudioDebug = new();

    /// <summary>Gets the ambient audio debug artifacts for the current async flow.</summary>
    public static AudioDebugArtifacts? GetAmbientAudioDebug() => _ambientAudioDebug.Value;

    /// <summary>Sets the ambient audio debug artifacts for the current async flow.</summary>
    /// <param name="originalBytes">The original audio bytes before transcoding.</param>
    /// <param name="originalMimeType">The MIME type of the original audio.</param>
    /// <param name="transcodedWav">The transcoded WAV bytes, or <see langword="null"/> if transcoding failed.</param>
    public static void SetAmbientAudioDebug(byte[] originalBytes, string originalMimeType, byte[]? transcodedWav) =>
        _ambientAudioDebug.Value = new AudioDebugArtifacts(originalBytes, originalMimeType, transcodedWav);

    /// <summary>Clears the ambient audio debug artifacts for the current async flow.</summary>
    public static void ClearAmbientAudioDebug() => _ambientAudioDebug.Value = null;

    /// <summary>Derives the OpenTelemetry activity source name by appending <c>.ai</c> to the metric name prefix.</summary>
    /// <remarks>
    /// Use the returned name in both <c>AddSource()</c> (tracing builder) and
    /// <c>UseOpenTelemetry(sourceName:)</c> (<see cref="ChatClientBuilder"/> pipeline) so that
    /// AI chat-completion spans are captured and exported.
    /// </remarks>
    /// <param name="metricNamePrefix">The <see cref="AppConfig.MetricNamePrefix"/> value (e.g. <c>"haus"</c>).</param>
    /// <returns>The source name with an <c>.ai</c> suffix (e.g. <c>"haus.ai"</c>).</returns>
    public static string GetAISourceName(string metricNamePrefix) => $"{metricNamePrefix}.ai";

    /// <summary>
    /// Creates an <see cref="IChatClient"/> and <see cref="AIAgent"/> from the specified
    /// <see cref="ProviderConfig"/> and <see cref="AgentConfig"/>.
    /// Standard logging and function-calling middleware are always applied to both pipelines;
    /// use the <paramref name="configureChatClient"/> and <paramref name="configureAgent"/> callbacks
    /// to insert additional middleware.
    /// </summary>
    /// <param name="provider">The infrastructure provider (connection, model, auth).</param>
    /// <param name="agentConfig">The agent behavioral configuration (instructions, prompt, reasoning).</param>
    /// <param name="httpClient">
    /// Optional pre-configured <see cref="HttpClient"/>. When <see langword="null"/> a default client targeting
    /// <see cref="ProviderConfig.Endpoint"/> with an infinite timeout is created.
    /// </param>
    /// <param name="tools">Optional list of AI tools to register with the agent.</param>
    /// <param name="configureChatClient">
    /// Optional callback to customise the <see cref="ChatClientBuilder"/> pipeline (e.g. adding middleware)
    /// before the client is built. <c>UseFunctionInvocation()</c> and standard logging
    /// middleware are always applied first.
    /// </param>
    /// <param name="configureAgent">
    /// Optional callback to customise the <see cref="AIAgentBuilder"/> pipeline (e.g. adding middleware)
    /// before the agent is built. Standard agent and function-calling middleware are always applied first.
    /// </param>
    /// <param name="instructionsAssembly">
    /// The assembly containing embedded instruction resources referenced by
    /// <see cref="AgentConfig.InstructionsSource"/>. When <see langword="null"/> the assembly
    /// containing <see cref="AgentExtensions"/> is used. Callers should pass their own assembly
    /// explicitly if <see cref="AgentExtensions"/> is relocated to a shared library.
    /// </param>
    /// <param name="aiConfig">
    /// Optional root AI configuration supplying shared <see cref="AIConfig.InstructionsPrefix"/>
    /// and <see cref="AIConfig.InstructionsSuffix"/>. When <see langword="null"/> no wrapping is applied.
    /// </param>
    /// <param name="otelSourceName">
    /// Optional OpenTelemetry activity source name for AI traces. When provided,
    /// <c>.UseOpenTelemetry(sourceName:)</c> is added to the <see cref="ChatClientBuilder"/>
    /// pipeline. Use <see cref="GetAISourceName"/> to derive from <see cref="AppConfig.MetricNamePrefix"/>.
    /// </param>
    /// <param name="tokenCredential">
    /// Optional <see cref="TokenCredential"/> for providers that use Azure Entra ID authentication
    /// (e.g. <see cref="AgentType.AzureOpenAI"/>). When <see langword="null"/>, key-based authentication
    /// via <see cref="ProviderConfig.ApiKey"/> is used instead.
    /// </param>
    /// <returns>A tuple of the built <see cref="IChatClient"/>, <see cref="AIAgent"/>, and the resolved system instructions.</returns>
    public static (IChatClient chatClient, AIAgent agent, string instructions) CreateAgent(
        ProviderConfig provider,
        AgentConfig agentConfig,
        HttpClient? httpClient = null,
        IEnumerable<AITool>? tools = null,
        Action<ChatClientBuilder>? configureChatClient = null,
        Action<AIAgentBuilder>? configureAgent = null,
        Assembly? instructionsAssembly = null,
        AIConfig? aiConfig = null,
        string? otelSourceName = null,
        TokenCredential? tokenCredential = null)
    {
        httpClient ??= new HttpClient
        {
            BaseAddress = provider.Endpoint,
            Timeout = Timeout.InfiniteTimeSpan,
        };

        ChatClientBuilder chatClientBuilder;
        if (provider.Type == AgentType.Ollama)
            chatClientBuilder = ((IChatClient)new OllamaApiClient(httpClient, provider.ModelName))
                .AsBuilder();
        else if (provider.Type == AgentType.AzureOpenAI)
        {
            var endpoint = provider.Endpoint
                ?? throw new InvalidOperationException(
                    $"Agent '{agentConfig.Name}' requires an {nameof(ProviderConfig.Endpoint)} for {nameof(AgentType.AzureOpenAI)}.");

            AzureOpenAIClient azureClient;
            if (tokenCredential is not null)
                azureClient = new AzureOpenAIClient(endpoint, tokenCredential,
                    new AzureOpenAIClientOptions { NetworkTimeout = Timeout.InfiniteTimeSpan });
            else if (provider.ApiKey is not null)
                azureClient = new AzureOpenAIClient(endpoint, new ApiKeyCredential(provider.ApiKey),
                    new AzureOpenAIClientOptions { NetworkTimeout = Timeout.InfiniteTimeSpan });
            else
                throw new InvalidOperationException(
                    $"Agent '{agentConfig.Name}' requires either a {nameof(TokenCredential)} or {nameof(ProviderConfig.ApiKey)} for {nameof(AgentType.AzureOpenAI)}.");

            chatClientBuilder = azureClient
                .GetChatClient(provider.ModelName)
                .AsIChatClient()
                .AsBuilder();
        }
        else if (provider.Type == AgentType.OpenAI)
        {
            var apiKey = provider.ApiKey
                ?? throw new InvalidOperationException(
                    $"Agent '{agentConfig.Name}' requires an {nameof(ProviderConfig.ApiKey)} for {nameof(AgentType.OpenAI)}.");

            chatClientBuilder = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
                {
                    Endpoint = provider.Endpoint,
                    NetworkTimeout = Timeout.InfiniteTimeSpan,
                })
                .GetChatClient(provider.ModelName)
                .AsIChatClient()
                .AsBuilder();
        }
        else
            throw new NotSupportedException($"Agent type '{provider.Type}' is not supported!");

        chatClientBuilder
            .Use(getResponseFunc: ChatResponseMiddleware, getStreamingResponseFunc: ChatStreamingResponseMiddleware)
            .UseFunctionInvocation();
        if (otelSourceName is not null)
            chatClientBuilder.UseOpenTelemetry(sourceName: otelSourceName);
        configureChatClient?.Invoke(chatClientBuilder);

        IChatClient chatClient = chatClientBuilder.Build();

        var instructions = ResolveInstructions(agentConfig,
            instructionsAssembly ?? typeof(AgentExtensions).Assembly, aiConfig);

        // Instructions are NOT set here — BuildChatOptions creates a separate per-request
        // ChatOptions with Instructions so the /instructions slash command (handled by
        // AgentCommandHandler.ApplyInstructionsOverride) can override them per-request.
        // The ChatOptions on agentOptions registers tools only.
        var agentOptions = new ChatClientAgentOptions
        {
            Name = agentConfig.Name,
            Description = agentConfig.Description,
            ChatOptions = new ChatOptions
            {
                Tools = tools?.ToList() ?? [],
            },
        };

        // Configure automatic chat history compaction when MaxMessages is set.
        // Uses ToolOutputStrippingChatReducer which drops FunctionCallContent/FunctionResultContent
        // messages and keeps a sliding window of the most recent exchanges — critical for reducing
        // context size on edge GPU devices.
        if (agentConfig.MaxMessages is > 0)
        {
            agentOptions.ChatHistoryProvider = new InMemoryChatHistoryProvider(
                new InMemoryChatHistoryProviderOptions
                {
                    ChatReducer = new ToolOutputStrippingChatReducer(agentConfig.MaxMessages.Value),
                });
            Log.Information("{ClassName} agent {AgentName} configured with automatic compaction (MaxMessages={MaxMessages})",
                nameof(AgentExtensions), agentConfig.Name, agentConfig.MaxMessages.Value);
        }

        var agentBuilder = new ChatClientAgent(chatClient, agentOptions)
            .AsBuilder()
            .Use(AgentRunMiddleware, AgentRunStreamingMiddleware)
            .Use(FunctionCallingMiddleware);

        configureAgent?.Invoke(agentBuilder);

        AIAgent agent = agentBuilder.Build();

        return (chatClient, agent, instructions);
    }

    /// <summary>
    /// Runs AI inference against the specified <see cref="AIAgent"/> using a pre-built <see cref="ChatMessage"/>,
    /// returning a formatted result string containing the model output, endpoint and duration.
    /// </summary>
    /// <param name="agent">The <see cref="AIAgent"/> to run inference against.</param>
    /// <param name="provider">The <see cref="ProviderConfig"/> providing model name and endpoint.</param>
    /// <param name="agentConfig">The <see cref="AgentConfig"/> providing agent name and prompt configuration.</param>
    /// <param name="message">The <see cref="ChatMessage"/> to send to the agent (use <see cref="BuildChatMessage"/> to create one).</param>
    /// <param name="chatOptions">The <see cref="ChatOptions"/> controlling reasoning effort and instructions (use <see cref="BuildChatOptions"/> to create).</param>
    /// <param name="session">An existing <see cref="AgentSession"/> to resume, or <see langword="null"/> to create a new session.</param>
    /// <param name="timeout">The timeout for the inference call. Defaults to 5 minutes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="AgentRunResult"/> containing the formatted output, the raw response messages,
    /// the elapsed duration and the (possibly new) <see cref="AgentSession"/>.
    /// </returns>
    public static async Task<AgentRunResult> RunAnalysisAsync(
        this AIAgent agent,
        ProviderConfig provider,
        AgentConfig agentConfig,
        ChatMessage message,
        ChatOptions chatOptions,
        AgentSession? session = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Log.Information("{ClassName} RunAnalysisAsync starting for agent {AgentName}, model={ModelName}, endpoint={Endpoint}",
            nameof(AgentExtensions), agentConfig.Name, provider.ModelName, provider.Endpoint.MaskEndpoint());

        // Set up ambient attachment accumulator so sub-agent tool invocations can bubble up images.
        var isTopLevel = _ambientAttachments.Value is null;
        if (isTopLevel)
            _ambientAttachments.Value = [];

        session ??= await agent.CreateSessionAsync(cancellationToken).AsTask();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? TimeSpan.FromMinutes(5));

        AgentRunOptions agentRunOptions = new ChatClientAgentRunOptions(chatOptions);

        // Save and reset per-call usage accumulator so nested sub-agent calls
        // (which also go through RunAnalysisAsync) don't clobber the parent's tally.
        var savedUsage = _accumulatedUsage.Value;
        _accumulatedUsage.Value = null;

        var response = await agent.RunAsync(message, session, agentRunOptions, timeoutCts.Token);

        var elapsed = sw.Elapsed;
        // Build output text from assistant text content only — exclude FunctionCallContent /
        // FunctionResultContent whose serialised payloads (e.g. base64 image bytes) would
        // otherwise leak into the user-facing message as garbled characters.
        var outputText = string.Join(" ", response.Messages
            .Where(m => m.Role == ChatRole.Assistant)
            .SelectMany(m => m.Contents.OfType<TextContent>())
            .Select(tc => tc.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t)));
        var formattedResult = $"Model: {provider.ModelName}" + Environment.NewLine
            + $"Endpoint: {provider.Endpoint.MaskEndpoint()}" + Environment.NewLine
            + $"Output: {outputText}" + Environment.NewLine
            + $"Duration: {elapsed}";

        var result = new AgentRunResult(agentConfig.Name)
        {
            FormattedResult = formattedResult,
            Elapsed = elapsed,
            Session = session,
            IsComplete = true,
            NestingDepth = _ambientDepth.Value,
            ProviderKey = agentConfig.Provider ?? string.Empty,
            ModelName = provider.ModelName ?? string.Empty,
        };
        result.AppendText(outputText);

        // Extract usage and tool-call count from the response messages.
        foreach (var msg in response.Messages)
        {
            if (msg.Contents is null)
                continue;
            foreach (var content in msg.Contents)
            {
                switch (content)
                {
                    case UsageContent uc when uc.Details is not null:
                        result.Usage = uc.Details;
                        break;
                    case FunctionCallContent fcc:
                        result.ToolCallCount++;
                        result.ToolCalls.Add(new ToolCallInfo(fcc.Name, fcc.Arguments));
                        break;
                    case FunctionResultContent frc:
                        ExtractImageAttachments(frc, result);
                        break;
                }
            }
        }

        // Fall back to accumulated usage from the middleware when message-level
        // UsageContent was not emitted or has no meaningful token counts.
        // The middleware accumulates from real provider responses, so it's more reliable
        // than UsageContent injected by FunctionInvocationChatClient's merged response.
        var messageUsage = result.Usage;
        var accumulated = _accumulatedUsage.Value;
        Log.Information("{ClassName} RunAnalysisAsync usage check for {AgentName}: messageUsage={HasMessageUsage} (in={MsgIn}, out={MsgOut}), accumulatedUsage={HasAccumulatedUsage} (in={AccIn}, out={AccOut})",
            nameof(AgentExtensions), agentConfig.Name,
            messageUsage is not null, messageUsage?.InputTokenCount, messageUsage?.OutputTokenCount,
            accumulated is not null, accumulated?.InputTokenCount, accumulated?.OutputTokenCount);

        // Prefer accumulated middleware usage when it has actual token data.
        if (accumulated is { InputTokenCount: > 0 } or { OutputTokenCount: > 0 })
        {
            result.Usage = accumulated;
            Log.Information("{ClassName} using accumulated middleware usage for {AgentName}: input={InputTokens}, output={OutputTokens}, total={TotalTokens}",
                nameof(AgentExtensions), agentConfig.Name,
                result.Usage.InputTokenCount,
                result.Usage.OutputTokenCount,
                result.Usage.TotalTokenCount);
        }
        else if (messageUsage is { InputTokenCount: > 0 } or { OutputTokenCount: > 0 })
            Log.Information("{ClassName} using message-level usage for {AgentName}: input={InputTokens}, output={OutputTokens}, total={TotalTokens}",
                nameof(AgentExtensions), agentConfig.Name,
                messageUsage.InputTokenCount,
                messageUsage.OutputTokenCount,
                messageUsage.TotalTokenCount);
        else
        {
            result.Usage = null; // Clear hollow UsageDetails with no token data.
            Log.Warning("{ClassName} no usage data available for {AgentName} (neither message-level nor accumulated had token counts)",
                nameof(AgentExtensions), agentConfig.Name);
        }

        // Restore the parent's accumulated usage so nested calls don't interfere.
        _accumulatedUsage.Value = savedUsage;

        // Drain ambient attachments accumulated by sub-agent tool invocations.
        if (isTopLevel && _ambientAttachments.Value is { Count: > 0 } ambient)
        {
            Log.Information("{ClassName} draining {AttachmentCount} ambient attachment(s) from sub-agent fan-out",
                nameof(AgentExtensions), ambient.Count);
            result.Attachments.AddRange(ambient);
            _ambientAttachments.Value = null;
        }

        Log.Information("{ClassName} RunAnalysisAsync completed for agent {AgentName} in {Duration}, toolCalls={ToolCallCount}, attachments={AttachmentCount}, outputLength={OutputLength}",
            nameof(AgentExtensions), agentConfig.Name, elapsed, result.ToolCallCount, result.Attachments.Count, outputText.Length);

        return result;
    }



    /// <summary>
    /// Inspects a <see cref="FunctionResultContent"/> for image-bearing payloads
    /// and extracts them as <see cref="AgentRunAttachment"/> entries on the result.
    /// </summary>
    private static void ExtractImageAttachments(FunctionResultContent frc, AgentRunResult result)
    {
        if (frc.Result is not JsonElement je || je.ValueKind is not JsonValueKind.Object)
            return;

        if (!je.TryGetProperty("hasImage", out var hasImageProp) || !hasImageProp.GetBoolean())
            return;

        if (!je.TryGetProperty("bytes", out var bytesProp) || bytesProp.ValueKind is not JsonValueKind.String)
            return;

        var base64 = bytesProp.GetString();
        if (string.IsNullOrEmpty(base64))
            return;

        var fileName = je.TryGetProperty("blobName", out var nameProp)
            ? nameProp.GetString()
            : null;

        var sizeKb = base64.Length * 3 / 4 / 1024;
        Log.Information("{ClassName} extracted image attachment {FileName} (~{SizeKb}KB) from tool result",
            nameof(AgentExtensions), fileName ?? "(unnamed)", sizeKb);

        result.Attachments.Add(new AgentRunAttachment
        {
            Base64Content = base64,
            MimeType = "image/jpeg",
            FileName = fileName,
        });
    }

    /// <summary>
    /// Builds <see cref="ChatOptions"/> from a <see cref="AgentConfig"/> with optional overrides
    /// for reasoning effort and system instructions.
    /// </summary>
    /// <param name="agentConfig">The agent configuration.</param>
    /// <param name="resolvedInstructions">
    /// The pre-resolved system instructions for the agent (obtained from <see cref="CreateAgent"/>
    /// or <see cref="ResolveInstructions"/>). Already includes prefix/suffix wrapping.
    /// </param>
    /// <param name="provider">Optional provider whose <see cref="ProviderConfig.ReasoningEffort"/> is applied when set.</param>
    /// <param name="instructionsOverride">When non-empty, replaces the agent-specific portion of the instructions for this call. Prefix/suffix from <paramref name="aiConfig"/> are still applied.</param>
    /// <param name="aiConfig">
    /// Optional root AI configuration supplying shared <see cref="AIConfig.InstructionsPrefix"/>
    /// and <see cref="AIConfig.InstructionsSuffix"/> to wrap <paramref name="instructionsOverride"/>.
    /// </param>
    public static ChatOptions BuildChatOptions(
        AgentConfig agentConfig,
        string resolvedInstructions,
        ProviderConfig? provider = null,
        string? instructionsOverride = null,
        AIConfig? aiConfig = null)
    {
        var opts = new ChatOptions
        {
            Instructions = !string.IsNullOrWhiteSpace(instructionsOverride)
                ? WrapInstructions(instructionsOverride, aiConfig)
                : resolvedInstructions,
        };

        if (provider?.ReasoningEffort is { } effort)
            opts.Reasoning = new ReasoningOptions { Effort = effort };

        return opts;
    }

    /// <summary>
    /// Builds a <see cref="ChatMessage"/> with <see cref="ChatRole.User"/> role from a text prompt
    /// and optional binary content (e.g. image bytes).
    /// </summary>
    /// <param name="prompt">The text prompt.</param>
    /// <param name="binaryContent">Optional binary payload to attach.</param>
    /// <param name="mimeType">MIME type of <paramref name="binaryContent"/> (e.g. "image/jpg"). Required when binary content is provided.</param>
    public static ChatMessage BuildChatMessage(string prompt, byte[]? binaryContent = null, string? mimeType = null)
    {
        var contents = new List<AIContent> { new TextContent(prompt) };
        if (binaryContent is not null && !string.IsNullOrWhiteSpace(mimeType))
            contents.Add(new DataContent(binaryContent, mimeType));

        return new ChatMessage(ChatRole.User, contents);
    }
}
