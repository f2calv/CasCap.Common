using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Agents.AI;
using ModelContextProtocol.Client;
using OllamaSharp;
using OpenAI;
using Serilog;
using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CasCap.Extensions;

/// <summary>
/// Extension methods for <see cref="AIAgent"/> that simplify creating agents and running inference
/// with text and optional binary content (e.g. images).
/// </summary>
public static class AgentExtensions
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
    /// Creates a long-lived <see cref="McpClient"/> connection and retrieves the available MCP tools
    /// from a remote MCP server over Streamable HTTP.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for disposing the returned <see cref="McpClient"/> when the tools
    /// are no longer needed. Disposing the client before tool invocation will cause
    /// <c>"Error: Function failed."</c> because the underlying transport is closed.
    /// </remarks>
    /// <param name="mcpEndpoint">The URL of the remote MCP server endpoint.</param>
    /// <returns>
    /// A tuple of the <see cref="McpClient"/> (which must be kept alive) and the list of
    /// <see cref="McpClientTool"/> instances representing the remote tools.
    /// </returns>
    public static async Task<(McpClient Client, List<McpClientTool> Tools)> GetHttpTools(string mcpEndpoint)
    {
        var mcpClient = await CreateMcpClientAsync(mcpEndpoint);
        var tools = (await mcpClient.ListToolsAsync()).ToList();
        foreach (var tool in tools)
            Log.Information("{ClassName} {Name} ({Description})", nameof(AgentExtensions), tool.Name, tool.Description);
        return (mcpClient, tools);
    }

    /// <summary>
    /// Creates a long-lived <see cref="McpClient"/> connection and retrieves the available MCP prompts
    /// from a remote MCP server over Streamable HTTP.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for disposing the returned <see cref="McpClient"/> when the prompts
    /// are no longer needed.
    /// </remarks>
    /// <param name="mcpEndpoint">The URL of the remote MCP server endpoint.</param>
    /// <returns>
    /// A tuple of the <see cref="McpClient"/> (which must be kept alive) and the list of
    /// <see cref="McpClientPrompt"/> instances representing the remote prompts.
    /// </returns>
    public static async Task<(McpClient Client, List<McpClientPrompt> Prompts)> GetHttpPrompts(string mcpEndpoint)
    {
        var mcpClient = await CreateMcpClientAsync(mcpEndpoint);
        var prompts = (await mcpClient.ListPromptsAsync()).ToList();
        foreach (var prompt in prompts)
            Log.Information("{ClassName} {Name} ({Description})", nameof(AgentExtensions), prompt.Name, prompt.Description);
        return (mcpClient, prompts);
    }

    /// <summary>
    /// Creates a new <see cref="McpClient"/> connected to a remote MCP server via Streamable HTTP.
    /// </summary>
    /// <param name="mcpEndpoint">The URL of the remote MCP server endpoint.</param>
    /// <returns>A connected <see cref="McpClient"/>. The caller owns disposal.</returns>
    private static Task<McpClient> CreateMcpClientAsync(string mcpEndpoint) =>
        McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            TransportMode = HttpTransportMode.StreamableHttp,
            Endpoint = new Uri(mcpEndpoint),
            ConnectionTimeout = Timeout.InfiniteTimeSpan,
            Name = $"{Environment.MachineName}-McpClient",
        }));

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
            nameof(AgentExtensions), agentConfig.Name, provider.ModelName, provider.Endpoint);

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
            + $"Endpoint: {provider.Endpoint}" + Environment.NewLine
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

    /// <summary>
    /// Scans <typeparamref name="TService"/> for public methods decorated with
    /// <see cref="McpServerToolAttribute"/> and wraps each one as an <see cref="AITool"/>.
    /// </summary>
    /// <typeparam name="TService">The service type whose methods will be wrapped as tools.</typeparam>
    /// <param name="serviceProvider">The service provider used to resolve <typeparamref name="TService"/>.</param>
    /// <param name="deferResolution">
    /// When <see langword="true"/>, defers service resolution to invocation time via
    /// <see cref="AIFunctionFactory.Create(MethodInfo, Func{AIFunctionArguments, object}, AIFunctionFactoryOptions?)"/>
    /// to avoid circular singleton dependencies.
    /// When <see langword="false"/> (default), resolves the service eagerly at creation time.
    /// </param>
    /// <returns>A list of <see cref="AITool"/> instances wrapping the discovered tool methods.</returns>
    public static List<AITool> CreateToolsFromServiceProvider<TService>(
        IServiceProvider serviceProvider, bool deferResolution = false)
        where TService : class =>
        CreateToolsFromServiceProvider(serviceProvider, typeof(TService), deferResolution);

    /// <summary>
    /// Scans <paramref name="serviceType"/> for public methods decorated with
    /// <see cref="McpServerToolAttribute"/> and wraps each one as an <see cref="AITool"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the service.</param>
    /// <param name="serviceType">The service type whose methods will be wrapped as tools.</param>
    /// <param name="deferResolution">
    /// When <see langword="true"/>, defers service resolution to invocation time to avoid circular singleton dependencies.
    /// When <see langword="false"/> (default), resolves the service eagerly at creation time.
    /// </param>
    /// <returns>A list of <see cref="AITool"/> instances wrapping the discovered tool methods.</returns>
    public static List<AITool> CreateToolsFromServiceProvider(
        IServiceProvider serviceProvider, Type serviceType, bool deferResolution = false)
    {
        var tools = new List<AITool>();
        var methods = serviceType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

        // Resolve eagerly unless deferred resolution is requested (e.g. to break circular singleton chains).
        object? target = deferResolution ? null : serviceProvider.GetRequiredService(serviceType);

        foreach (var method in methods)
        {
            var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var options = new AIFunctionFactoryOptions
            {
                Name = method.Name.ToSnakeCase(),
                Description = description,
            };

            var aiFunction = deferResolution
                ? AIFunctionFactory.Create(method, (AIFunctionArguments _) => serviceProvider.GetRequiredService(serviceType), options)
                : AIFunctionFactory.Create(method, target, options);

            tools.Add(aiFunction);
        }

        return tools;
    }

    /// <summary>
    /// Resolves all tool sources declared in <see cref="AgentConfig.Tools"/> and returns
    /// the combined, filtered list of <see cref="AITool"/> instances.
    /// </summary>
    /// <remarks>
    /// Three source types are supported:
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="ToolSource.Service"/> – scans loaded assemblies for the named
    /// <c>*McpQueryService</c> type and wraps its <see cref="McpServerToolAttribute"/>-decorated
    /// methods.
    /// </description></item>
    /// <item><description>
    /// <see cref="ToolSource.Agent"/> – wraps a peer <see cref="AIAgent"/> (resolved from the DI
    /// container by key) as a single callable <see cref="AITool"/> using the fan-out pattern.
    /// Requires <paramref name="aiConfig"/> to look up the target agent's config and provider.
    /// </description></item>
    /// </list>
    /// Include/exclude filters from each <see cref="ToolSource"/> are applied after source
    /// resolution. HTTP endpoint sources (<see cref="ToolSource.Endpoint"/>) are handled
    /// separately at startup and are not processed here.
    /// </remarks>
    /// <param name="serviceProvider">The service provider used to resolve each tool service.</param>
    /// <param name="agentConfig">The agent whose <see cref="AgentConfig.Tools"/> are resolved.</param>
    /// <param name="aiConfig">
    /// The full AI configuration. Required when any <see cref="ToolSource.Agent"/> entry is
    /// present so that the target agent's <see cref="AgentConfig"/> and <see cref="ProviderConfig"/>
    /// can be looked up.
    /// </param>
    /// <param name="deferResolution">
    /// When <see langword="true"/>, defers service resolution to invocation time.
    /// When <see langword="false"/> (default), resolves eagerly.
    /// </param>
    /// <param name="isDevelopment">
    /// When <see langword="true"/>, throws an <see cref="InvalidOperationException"/> for misconfigured
    /// tool filters; when <see langword="false"/> (default), logs warnings instead.
    /// </param>
    /// <param name="instructionsAssembly">
    /// The assembly containing embedded instruction resources. Forwarded to sub-agent tool
    /// creation so that <see cref="AgentConfig.InstructionsSource"/> resolves correctly.
    /// When <see langword="null"/> the assembly containing <see cref="AgentExtensions"/> is used.
    /// </param>
    /// <returns>A combined list of <see cref="AITool"/> instances from all declared tool sources.</returns>
    public static List<AITool> CreateToolsForAgent(
        IServiceProvider serviceProvider, AgentConfig agentConfig, AIConfig? aiConfig = null,
        bool deferResolution = false, bool isDevelopment = false, Assembly? instructionsAssembly = null)
    {
        var tools = new List<AITool>();

        var isServiceChecker = serviceProvider.GetService<IServiceProviderIsService>();

        foreach (var source in agentConfig.Tools.Where(s => s.Service is not null))
        {
            var serviceType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException) { return []; }
                })
                .FirstOrDefault(t => t.Name == source.Service && !t.IsAbstract)
                ?? throw new InvalidOperationException(
                    $"Tool service type '{source.Service}' not found in any loaded assembly.");

            // Fail fast when the backing service is not registered in DI — this happens
            // when the feature that registers the service is disabled for this deployment.
            if (deferResolution && isServiceChecker is not null && !isServiceChecker.IsService(serviceType))
                throw new InvalidOperationException(
                    $"Tool service '{source.Service}' is declared in agent config but not registered in DI. "
                    + "Ensure the feature that registers this service is enabled.");

            tools.AddRange(FilterTools(
                CreateToolsFromServiceProvider(serviceProvider, serviceType, deferResolution), source, isDevelopment));
        }

        foreach (var source in agentConfig.Tools.Where(s => s.Agent is not null))
        {
            if (aiConfig is null)
                throw new InvalidOperationException(
                    $"AIConfig must be provided to resolve agent tool source '{source.Agent}'.");

            if (!aiConfig.Agents.TryGetValue(source.Agent!, out var targetAgentConfig))
                throw new InvalidOperationException(
                    $"Agent '{source.Agent}' referenced in tool source not found in AIConfig.Agents.");

            if (!targetAgentConfig.Enabled)
            {
                Log.Information("{ClassName} skipping disabled sub-agent {AgentKey}", nameof(AgentExtensions), source.Agent);
                continue;
            }

            if (!aiConfig.Providers.TryGetValue(targetAgentConfig.Provider, out var targetProvider))
                throw new InvalidOperationException(
                    $"Provider '{targetAgentConfig.Provider}' for agent '{source.Agent}' not found in AIConfig.Providers.");

            var agentTool = CreateAgentTool(serviceProvider, source.Agent!, targetAgentConfig, targetProvider, aiConfig, instructionsAssembly);
            tools.AddRange(FilterTools([agentTool], source, isDevelopment));
        }

        return tools;
    }

    /// <summary>
    /// Wraps a peer <see cref="AIAgent"/> as a single callable <see cref="AITool"/>, enabling
    /// the fan-out / agent-delegation pattern where a parent agent can invoke a specialist agent
    /// as one of its tools.
    /// </summary>
    /// <remarks>
    /// The produced tool is named <c>invoke_{agentKey_snake_case}</c> (e.g.
    /// <c>invoke_security_agent</c> for <c>"SecurityAgent"</c>). When invoked, it resolves the
    /// target agent lazily from the DI container, runs it with the supplied task text, and
    /// returns the output text. A fresh stateless session is used for each invocation.
    /// </remarks>
    /// <param name="serviceProvider">Used to resolve the target <see cref="AIAgent"/> at invocation time.</param>
    /// <param name="agentKey">The DI key (and <see cref="AIConfig.Agents"/> key) of the target agent.</param>
    /// <param name="agentConfig">Configuration of the target agent, used for the tool description and chat options.</param>
    /// <param name="providerConfig">Infrastructure provider for the target agent, used during inference.</param>
    /// <param name="aiConfig">Optional root AI configuration for instruction prefix/suffix wrapping.</param>
    /// <param name="instructionsAssembly">Optional assembly containing embedded instruction resources for the agent.</param>
    /// <returns>An <see cref="AITool"/> that delegates to the named agent.</returns>
    public static AITool CreateAgentTool(
        IServiceProvider serviceProvider,
        string agentKey,
        AgentConfig agentConfig,
        ProviderConfig providerConfig,
        AIConfig? aiConfig = null,
        Assembly? instructionsAssembly = null)
    {
        async Task<string> InvokeAgent(
            [Description("The task, question or event description to pass to this specialist agent.")] string task,
            [Description("Set to true to forward the current binary attachment (e.g. audio file) to this agent. Only set when the parent message includes a file the sub-agent needs to process.")] bool forwardAttachment = false,
            CancellationToken cancellationToken = default)
        {
            var depth = _ambientDepth.Value + 1;
            Log.Information("{ClassName} delegating to sub-agent {AgentKey} (depth={NestingDepth}, forwardAttachment={ForwardAttachment}): {Task}",
                nameof(AgentExtensions), agentKey, depth, forwardAttachment, task);

            // Fire the ambient delegation callback (e.g. send status message / swap reaction).
            if (_delegationCallback.Value is { } callback)
                await callback(agentKey, depth, providerConfig, cancellationToken);

            _ambientDepth.Value = depth;
            var agent = serviceProvider.GetRequiredKeyedService<AIAgent>(agentKey);

            // Forward the parent's binary attachment when requested and available.
            byte[]? binaryContent = null;
            string? mimeType = null;
            if (forwardAttachment && _ambientBinaryContent.Value is { } ambient)
            {
                binaryContent = ambient.Bytes;
                mimeType = ambient.MimeType;

                // Transcode non-WAV audio to WAV via ffmpeg (stdin→stdout, no temp files).
                // Whisper models expect decoded PCM/WAV; Signal sends raw AAC streams which
                // most STT models cannot decode directly.
                if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                    && !mimeType.Equals("audio/wav", StringComparison.OrdinalIgnoreCase)
                    && !mimeType.Equals("audio/x-wav", StringComparison.OrdinalIgnoreCase))
                {
                    var originalBytes = binaryContent;
                    var originalMimeType = mimeType;
                    var transcoded = await TranscodeToWavAsync(binaryContent, cancellationToken);
                    if (transcoded is not null)
                    {
                        Log.Information("{ClassName} transcoded {OriginalSize} byte {OriginalMimeType} → {TranscodedSize} byte WAV for {AgentKey}",
                            nameof(AgentExtensions), binaryContent.Length, mimeType, transcoded.Length, agentKey);
                        binaryContent = transcoded;
                        mimeType = "audio/wav";
                    }
                    else
                    {
                        Log.Warning("{ClassName} ffmpeg transcode failed, forwarding original {MimeType} bytes to {AgentKey}",
                            nameof(AgentExtensions), mimeType, agentKey);
                    }
                }

                // OllamaSharp only maps DataContent with image/* MIME types to the Ollama API
                // images array (see AbstractionMapper.ToOllamaSharpMessages). Non-image content
                // is silently dropped. Override the MIME type so the bytes reach the model —
                // Ollama's images field is a raw base64 array and Whisper models interpret the
                // payload as audio regardless of the transport-level MIME label.
                if (!mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("{ClassName} overriding MIME type {OriginalMimeType} → image/png for OllamaSharp transport to {AgentKey}",
                        nameof(AgentExtensions), mimeType, agentKey);
                    mimeType = "image/png";
                }

                Log.Information("{ClassName} forwarding {Size} byte attachment ({MimeType}) to sub-agent {AgentKey}",
                    nameof(AgentExtensions), binaryContent.Length, mimeType, agentKey);
            }
            else if (forwardAttachment)
                Log.Warning("{ClassName} forwardAttachment=true but no ambient binary content available for {AgentKey}",
                    nameof(AgentExtensions), agentKey);

            var message = BuildChatMessage(task, binaryContent: binaryContent, mimeType: mimeType);
            var resolvedInstructions = ResolveInstructions(agentConfig, instructionsAssembly, aiConfig);
            var chatOptions = BuildChatOptions(agentConfig, resolvedInstructions);
            var result = await agent.RunAnalysisAsync(providerConfig, agentConfig, message, chatOptions,
                cancellationToken: cancellationToken);

            _ambientDepth.Value = depth - 1;

            Log.Information("{ClassName} sub-agent {AgentKey} completed in {Duration}, toolCalls={ToolCallCount}, attachments={AttachmentCount}",
                nameof(AgentExtensions), agentKey, result.Elapsed, result.ToolCallCount, result.Attachments.Count);

            // Fire the ambient completion callback (e.g. send debug stats for this sub-agent step).
            if (_completionCallback.Value is { } completionCb)
                await completionCb(agentKey, depth, result, cancellationToken);

            // Bubble up any image attachments from the sub-agent to the parent's ambient collector.
            if (result.Attachments.Count > 0)
            {
                Log.Information("{ClassName} bubbling {AttachmentCount} attachment(s) from sub-agent {AgentKey} to parent",
                    nameof(AgentExtensions), result.Attachments.Count, agentKey);
                _ambientAttachments.Value?.AddRange(result.Attachments);
            }

            return result.OutputText;
        }

        return AIFunctionFactory.Create(InvokeAgent, new AIFunctionFactoryOptions
        {
            Name = $"invoke_{agentKey.ToSnakeCase()}",
            Description = agentConfig.Description,
        });
    }

    /// <summary>
    /// Applies <see cref="ToolSource.IncludeTools"/> and <see cref="ToolSource.ExcludeTools"/>
    /// filters to a list of <see cref="AITool"/> instances.
    /// </summary>
    /// <param name="tools">The unfiltered tool list from a single source.</param>
    /// <param name="source">The <see cref="ToolSource"/> carrying the filter arrays.</param>
    /// <param name="isDevelopment">
    /// When <see langword="true"/>, throws an <see cref="InvalidOperationException"/> listing
    /// all misconfigured tool names; when <see langword="false"/>, logs warnings instead.
    /// </param>
    /// <returns>The filtered tool list.</returns>
    public static IEnumerable<AITool> FilterTools(IEnumerable<AITool> tools, ToolSource source,
        bool isDevelopment = false)
    {
        var toolList = tools.ToList();
        var availableNames = toolList.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var misconfigured = new List<string>();

        if (source.IncludeTools.Length > 0)
        {
            foreach (var name in source.IncludeTools)
                if (!availableNames.Contains(name))
                    misconfigured.Add($"included tool '{name}' not found");
            toolList = toolList.Where(t => source.IncludeTools.Contains(t.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        if (source.ExcludeTools.Length > 0)
        {
            foreach (var name in source.ExcludeTools)
                if (!availableNames.Contains(name))
                    misconfigured.Add($"excluded tool '{name}' not found");
            toolList = toolList.Where(t => !source.ExcludeTools.Contains(t.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        ReportMisconfigured(misconfigured, "tools", isDevelopment);

        foreach (var tool in toolList)
            Log.Information("{ClassName} enabled tool {ToolName}", nameof(AgentExtensions), tool.Name);

        return toolList;
    }

    /// <summary>
    /// Resolves all in-process prompt sources declared in <see cref="AgentConfig.Prompts"/>
    /// (where <see cref="PromptSource.Service"/> is set) by scanning loaded assemblies for
    /// matching type names decorated with <see cref="McpServerPromptTypeAttribute"/> and
    /// discovering their <see cref="McpServerPromptAttribute"/>-decorated methods as
    /// <see cref="McpPromptDescriptor"/> instances. Include/exclude filters from each
    /// <see cref="PromptSource"/> are applied to the resulting prompt list.
    /// </summary>
    /// <param name="agentConfig">The agent whose <see cref="AgentConfig.Prompts"/> are resolved.</param>
    /// <param name="isDevelopment">
    /// When <see langword="true"/>, throws an <see cref="InvalidOperationException"/> for misconfigured
    /// prompt filters; when <see langword="false"/> (default), logs warnings instead.
    /// </param>
    /// <returns>A combined list of <see cref="McpPromptDescriptor"/> instances from all declared service-based prompt sources.</returns>
    public static List<McpPromptDescriptor> CreatePromptsForAgent(AgentConfig agentConfig,
        bool isDevelopment = false)
    {
        var prompts = new List<McpPromptDescriptor>();

        foreach (var source in agentConfig.Prompts.Where(s => s.Service is not null))
        {
            var promptType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException) { return []; }
                })
                .FirstOrDefault(t => t.Name == source.Service
                    && t.GetCustomAttribute<McpServerPromptTypeAttribute>() is not null)
                ?? throw new InvalidOperationException(
                    $"Prompt type '{source.Service}' not found in any loaded assembly.");

            var methods = promptType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<McpServerPromptAttribute>() is not null);

            var sourcePrompts = new List<McpPromptDescriptor>();
            foreach (var method in methods)
            {
                var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
                var parameters = method.GetParameters()
                    .Select(p => new McpPromptDescriptor.Parameter
                    {
                        Name = p.Name ?? p.Position.ToString(),
                        Description = p.GetCustomAttribute<DescriptionAttribute>()?.Description,
                        Required = !p.HasDefaultValue,
                    })
                    .ToList();

                sourcePrompts.Add(new McpPromptDescriptor
                {
                    Name = method.Name,
                    Description = description,
                    Parameters = parameters,
                });

                Log.Information("{ClassName} in-process prompt {Name} ({Description})",
                    nameof(AgentExtensions), method.Name, description);
            }

            prompts.AddRange(FilterPrompts(sourcePrompts, source, isDevelopment));
        }

        return prompts;
    }

    /// <summary>
    /// Applies <see cref="PromptSource.IncludePrompts"/> and <see cref="PromptSource.ExcludePrompts"/>
    /// filters to a list of <see cref="McpPromptDescriptor"/> instances.
    /// </summary>
    /// <param name="prompts">The unfiltered prompt list from a single source.</param>
    /// <param name="source">The <see cref="PromptSource"/> carrying the filter arrays.</param>
    /// <param name="isDevelopment">
    /// When <see langword="true"/>, throws an <see cref="InvalidOperationException"/> listing
    /// all misconfigured prompt names; when <see langword="false"/>, logs warnings instead.
    /// </param>
    /// <returns>The filtered prompt list.</returns>
    public static IEnumerable<McpPromptDescriptor> FilterPrompts(
        IEnumerable<McpPromptDescriptor> prompts, PromptSource source, bool isDevelopment = false)
    {
        var promptList = prompts.ToList();
        var availableNames = promptList.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var misconfigured = new List<string>();

        if (source.IncludePrompts.Length > 0)
        {
            foreach (var name in source.IncludePrompts)
                if (!availableNames.Contains(name))
                    misconfigured.Add($"included prompt '{name}' not found");
            promptList = promptList.Where(p => source.IncludePrompts.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        if (source.ExcludePrompts.Length > 0)
        {
            foreach (var name in source.ExcludePrompts)
                if (!availableNames.Contains(name))
                    misconfigured.Add($"excluded prompt '{name}' not found");
            promptList = promptList.Where(p => !source.ExcludePrompts.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        ReportMisconfigured(misconfigured, "prompts", isDevelopment);

        foreach (var prompt in promptList)
            Log.Information("{ClassName} enabled prompt {PromptName}", nameof(AgentExtensions), prompt.Name);

        return promptList;
    }

    /// <summary>
    /// Reports misconfigured filter names by throwing in development or logging warnings in production.
    /// </summary>
    private static void ReportMisconfigured(List<string> misconfigured, string category, bool isDevelopment)
    {
        if (misconfigured.Count == 0)
            return;

        var message = $"{nameof(AgentExtensions)} misconfigured {category}: {string.Join("; ", misconfigured)}";
        if (isDevelopment)
            throw new InvalidOperationException(message);

        Log.Warning("{ClassName} misconfigured {Category}: {Details}",
            nameof(AgentExtensions), category, string.Join("; ", misconfigured));
    }

    /// <summary>
    /// Converts a sequence of remote <see cref="McpClientPrompt"/> instances to
    /// <see cref="McpPromptDescriptor"/> instances for unified prompt handling.
    /// </summary>
    /// <param name="mcpPrompts">The remote prompts returned by <c>ListPromptsAsync</c>.</param>
    /// <returns>A list of <see cref="McpPromptDescriptor"/> mapped from the remote prompts.</returns>
    public static List<McpPromptDescriptor> ToPromptDescriptors(this IEnumerable<McpClientPrompt> mcpPrompts) =>
        mcpPrompts.Select(p => new McpPromptDescriptor
        {
            Name = p.Name,
            Description = p.Description,
            Parameters = (p.ProtocolPrompt.Arguments ?? []).Select(a => new McpPromptDescriptor.Parameter
            {
                Name = a.Name,
                Description = a.Description,
                Required = a.Required ?? false,
            }).ToList(),
        }).ToList();

    /// <summary>
    /// Resolves the system instructions for an <see cref="AgentConfig"/> by reading from
    /// <see cref="AgentConfig.InstructionsSource"/> when set — first as an embedded resource name,
    /// then as an absolute file system path — falling back to <see cref="AgentConfig.Instructions"/>.
    /// When <paramref name="aiConfig"/> is supplied, <see cref="AIConfig.InstructionsPrefix"/> and
    /// <see cref="AIConfig.InstructionsSuffix"/> are prepended and appended respectively.
    /// </summary>
    /// <param name="agentConfig">The agent configuration to resolve instructions for.</param>
    /// <param name="instructionsAssembly">
    /// The assembly containing embedded instruction resources. When <see langword="null"/>
    /// the assembly containing <see cref="AgentExtensions"/> is used.
    /// </param>
    /// <param name="aiConfig">
    /// Optional root AI configuration supplying shared <see cref="AIConfig.InstructionsPrefix"/>
    /// and <see cref="AIConfig.InstructionsSuffix"/>. When <see langword="null"/> no wrapping is applied.
    /// </param>
    /// <returns>The resolved, non-empty instructions string.</returns>
    /// <exception cref="FileNotFoundException">
    /// <see cref="AgentConfig.InstructionsSource"/> is set but could not be found as an embedded resource or file.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Neither <see cref="AgentConfig.Instructions"/> nor <see cref="AgentConfig.InstructionsSource"/> yields a value.
    /// </exception>
    public static string ResolveInstructions(AgentConfig agentConfig, Assembly? instructionsAssembly = null, AIConfig? aiConfig = null)
    {
        var instructions = agentConfig.Instructions;
        if (!string.IsNullOrWhiteSpace(agentConfig.InstructionsSource))
        {
            var source = agentConfig.InstructionsSource;

            // 1) Try embedded resource.
            instructions = (instructionsAssembly ?? typeof(AgentExtensions).Assembly)
                .GetManifestResourceString(source);

            // 2) Fall back to file system.
            if (string.IsNullOrWhiteSpace(instructions) && File.Exists(source))
                instructions = File.ReadAllText(source);

            if (string.IsNullOrWhiteSpace(instructions))
                throw new FileNotFoundException(
                    $"InstructionsSource '{source}' not found as an embedded resource or file.", source);
        }
        if (string.IsNullOrWhiteSpace(instructions))
            throw new NotSupportedException(
                $"either {nameof(agentConfig.Instructions)} or {nameof(agentConfig.InstructionsSource)} must be set");
        return WrapInstructions(instructions, aiConfig);
    }

    /// <summary>
    /// Wraps agent-specific instructions with the shared <see cref="AIConfig.InstructionsPrefix"/>
    /// and <see cref="AIConfig.InstructionsSuffix"/> when configured.
    /// </summary>
    internal static string WrapInstructions(string agentInstructions, AIConfig? aiConfig)
    {
        if (aiConfig is null)
            return agentInstructions;

        var prefix = aiConfig.InstructionsPrefix;
        var suffix = aiConfig.InstructionsSuffix;
        if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(suffix))
            return agentInstructions;

        return string.Concat(
            string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix + " ",
            agentInstructions,
            string.IsNullOrWhiteSpace(suffix) ? string.Empty : " " + suffix);
    }

    #region middleware

    /// <summary>
    /// Chat-client-level response middleware that logs the inbound request message count and outbound response message count.
    /// </summary>
    internal static async Task<ChatResponse> ChatResponseMiddleware(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        CancellationToken cancellationToken)
    {
        Log.Information("{ClassName} chat request, messageCount={Count}", nameof(AgentExtensions), messages.Count());
        var response = await innerClient.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        Log.Information("{ClassName} chat response, messageCount={Count}, hasUsage={HasUsage}, inputTokens={InputTokens}, outputTokens={OutputTokens}",
            nameof(AgentExtensions), response.Messages.Count,
            response.Usage is not null,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount);

        // Accumulate usage across multiple chat round-trips (tool-call loops).
        // ChatClientAgent calls IChatClient.GetResponseAsync for each round-trip,
        // but only the Messages are surfaced through AgentRunResponse — the
        // ChatResponse.Usage property is lost. We accumulate it here via AsyncLocal
        // so RunAnalysisAsync can read the aggregate.
        if (response.Usage is not null)
        {
            var prev = _accumulatedUsage.Value;
            _accumulatedUsage.Value = new UsageDetails
            {
                InputTokenCount = (prev?.InputTokenCount ?? 0) + (response.Usage.InputTokenCount ?? 0),
                OutputTokenCount = (prev?.OutputTokenCount ?? 0) + (response.Usage.OutputTokenCount ?? 0),
                TotalTokenCount = (prev?.TotalTokenCount ?? 0) + (response.Usage.TotalTokenCount ?? 0),
            };
        }

        return response;
    }

    /// <summary>
    /// Chat-client-level streaming response middleware that logs the inbound request message count and total streamed update count.
    /// Also accumulates <see cref="UsageContent"/> from streamed updates into <see cref="_accumulatedUsage"/>
    /// so that <see cref="RunAnalysisAsync"/> can report token counts even when the agent framework uses streaming internally.
    /// </summary>
    internal static async IAsyncEnumerable<ChatResponseUpdate> ChatStreamingResponseMiddleware(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Log.Debug("{ClassName} streaming chat message count={Count}", nameof(AgentExtensions), messages.Count());
        var updateCount = 0;
        await foreach (var update in innerClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updateCount++;

            // Capture usage from streaming updates (typically the final chunk contains token counts).
            if (update.Contents is not null)
            {
                foreach (var content in update.Contents)
                {
                    if (content is UsageContent uc && uc.Details is not null)
                    {
                        var prev = _accumulatedUsage.Value;
                        _accumulatedUsage.Value = new UsageDetails
                        {
                            InputTokenCount = (prev?.InputTokenCount ?? 0) + (uc.Details.InputTokenCount ?? 0),
                            OutputTokenCount = (prev?.OutputTokenCount ?? 0) + (uc.Details.OutputTokenCount ?? 0),
                            TotalTokenCount = (prev?.TotalTokenCount ?? 0) + (uc.Details.TotalTokenCount ?? 0),
                        };
                        Log.Debug("{ClassName} streaming usage captured: input={InputTokens}, output={OutputTokens}",
                            nameof(AgentExtensions), uc.Details.InputTokenCount, uc.Details.OutputTokenCount);
                    }
                }
            }

            yield return update;
        }
        Log.Debug("{ClassName} streaming chat update count={Count}", nameof(AgentExtensions), updateCount);
    }

    /// <summary>
    /// Agent run middleware that logs the inbound message count and outbound response message count.
    /// </summary>
    /// <remarks>
    /// See <see href="https://learn.microsoft.com/en-us/agent-framework/agents/middleware/?pivots=programming-language-csharp">Agent middleware documentation</see>.
    /// </remarks>
    internal static async Task<AgentResponse> AgentRunMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        Log.Information("{ClassName} agent run, messageCount={Count}", nameof(AgentExtensions), messages.Count());
        var response = await innerAgent.RunAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
        Log.Information("{ClassName} agent response, messageCount={Count}", nameof(AgentExtensions), response.Messages.Count);
        return response;
    }

    /// <summary>
    /// Agent run streaming middleware that logs the inbound message count and total streamed update count.
    /// </summary>
    internal static async IAsyncEnumerable<AgentResponseUpdate> AgentRunStreamingMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Log.Debug("{ClassName} streaming agent message count={Count}", nameof(AgentExtensions), messages.Count());
        List<AgentResponseUpdate> updates = [];
        await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            updates.Add(update);
            yield return update;
        }
        Log.Debug("{ClassName} streaming agent update count={Count}", nameof(AgentExtensions), updates.ToAgentResponse().Messages.Count);
    }

    /// <summary>
    /// Function calling middleware that logs the function name, arguments and result for each tool invocation.
    /// </summary>
    internal static async ValueTask<object?> FunctionCallingMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        StringBuilder sb = new();
        sb.Append($"Tool Call: '{context.Function.Name}'");
        if (context.Arguments.Count > 0)
            sb.Append($" (Args: {string.Join(",", context.Arguments.Select(x => $"[{x.Key} = {x.Value}]"))})");
        Log.Information("{ClassName} {FunctionCallDetails}",
            nameof(AgentExtensions), sb);

        object? result;
        try
        {
            result = await next(context, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{ClassName} Function {FunctionName} threw an exception", nameof(AgentExtensions), context.Function.Name);
            throw;
        }
        var resultPreview = result switch
        {
            string s when s.Length > 500 => $"{s[..500]}... ({s.Length} chars)",
            JsonElement je => je.ToString().Length > 500
                ? $"{je.ToString()[..500]}... ({je.ToString().Length} chars)"
                : je.ToString(),
            _ => result?.ToString()
        };
        Log.Debug("{ClassName} Function Call Result: {Result}",
            nameof(AgentExtensions), resultPreview);

        return result;
    }

    #endregion

    /// <summary>
    /// Transcodes audio bytes to 16-bit PCM WAV via <c>ffmpeg</c> (stdin → stdout, no temp files).
    /// Returns <see langword="null"/> if <c>ffmpeg</c> is not installed or the process fails.
    /// </summary>
    public static async Task<byte[]?> TranscodeToWavAsync(byte[] inputBytes, CancellationToken cancellationToken)
    {
        // -i pipe:0        read from stdin
        // -f wav           output WAV format
        // -ar 16000        16 kHz sample rate (Whisper native)
        // -ac 1            mono
        // -sample_fmt s16  16-bit PCM
        // pipe:1           write to stdout
        const string args = "-i pipe:0 -f wav -ar 16000 -ac 1 -sample_fmt s16 pipe:1 -loglevel error";

        var (output, error, exitCode) = await ShellExtensions.RunProcessWithStdinAsync(
            "ffmpeg", args, inputBytes, cancellationToken);

        if (exitCode != 0)
        {
            Log.Warning("{ClassName} ffmpeg exited with code {ExitCode}: {StdErr}",
                nameof(AgentExtensions), exitCode, error);
            return null;
        }

        return output.Length > 0 ? output : null;
    }
}
