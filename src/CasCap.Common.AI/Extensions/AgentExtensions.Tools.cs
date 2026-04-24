namespace CasCap.Extensions;

/// <summary>Tool discovery, filtering, and sub-agent delegation tool creation.</summary>
public static partial class AgentExtensions
{
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
}
