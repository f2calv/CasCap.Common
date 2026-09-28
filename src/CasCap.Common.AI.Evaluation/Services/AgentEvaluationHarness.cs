using Azure.Core;

namespace CasCap.Common.Services;

/// <summary>
/// Runs an evaluation scenario through the production agent factory against a chosen model, with
/// fixture-backed tools and a recorder on every model request.
/// </summary>
/// <remarks>
/// Agents are created with <see cref="AgentExtensions.CreateAgent"/> and run with
/// <see cref="AgentExtensions.RunAnalysisAsync"/>, and every agent in the delegation tree is rebuilt against
/// the model under test. Nothing reaches a real system: query tools return fixtures and side-effect tools
/// are recorded but never executed. Run scenarios sequentially; a shared model server and the
/// <see cref="AzureThrottlingMonitor"/> both assume one run at a time.
/// </remarks>
/// <param name="loggerFactory">Routes agent diagnostics to the caller's logging.</param>
/// <param name="aiConfig">Supplies agent definitions and providers.</param>
/// <param name="toolCatalog">The application's MCP tool types.</param>
/// <param name="instructionsAssembly">The assembly holding embedded <see cref="AgentConfig.InstructionsSource"/> resources.</param>
/// <param name="tokenCredential">Entra ID credential for Azure OpenAI providers, or <see langword="null"/>.</param>
/// <param name="config">Evaluation switches, or <see langword="null"/> for the defaults.</param>
/// <param name="throttlingMonitor">Counts Azure SDK throttling per run, or <see langword="null"/> to skip.</param>
public sealed class AgentEvaluationHarness(
    ILoggerFactory loggerFactory,
    AIConfig aiConfig,
    McpToolCatalog toolCatalog,
    Assembly instructionsAssembly,
    TokenCredential? tokenCredential = null,
    AgentEvaluationConfig? config = null,
    AzureThrottlingMonitor? throttlingMonitor = null)
{
    private const string WarmUpPrompt = "Reply with the single word OK.";

    /// <summary>The MCP tool types the harness builds agents from.</summary>
    public McpToolCatalog ToolCatalog => toolCatalog;

    /// <summary>
    /// Returns why a provider cannot be evaluated on this machine, or <see langword="null"/> when it can.
    /// </summary>
    /// <param name="providerKey">Key into <c>CasCap:AIConfig:Providers</c>.</param>
    public string? GetUnavailableReason(string providerKey)
    {
        if (!aiConfig.Providers.TryGetValue(providerKey, out var provider))
            return "not defined in CasCap:AIConfig:Providers";

        return provider.Type switch
        {
            AgentType.AzureAIFoundry => $"{nameof(AgentType.AzureAIFoundry)} is not supported by CasCap.Common.AI",
            AgentType.AzureOpenAI or AgentType.Ollama when provider.Endpoint is null => "no Endpoint configured",
            AgentType.AzureOpenAI when tokenCredential is null && string.IsNullOrWhiteSpace(provider.ApiKey)
                => "no Entra ID credential or ApiKey configured",
            AgentType.OpenAI when string.IsNullOrWhiteSpace(provider.ApiKey) => "no ApiKey configured",
            _ => null,
        };
    }

    /// <summary>
    /// Returns the model-facing names of every tool a scenario could reference: all catalogued tools, every
    /// delegation tool and the candidate tools of the given variants.
    /// </summary>
    /// <param name="toolSurfaceVariants">Variants whose candidate tools are included.</param>
    public IReadOnlySet<string> GetKnownToolNames(IEnumerable<ToolSurfaceVariant> toolSurfaceVariants) =>
        toolCatalog.GetAllTools().Select(t => t.Name)
            .Concat(aiConfig.Agents.Keys.Select(k => $"invoke_{k.ToSnakeCase()}"))
            .Concat(toolSurfaceVariants.SelectMany(v => v.CandidateTools.Values.SelectMany(t => t)).Select(t => t.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sends one tool-free request so a model router has the model resident before measured runs start,
    /// keeping model swap time out of the latency figures. Call it twice to measure plain chat latency.
    /// </summary>
    /// <param name="providerKey">Key into <c>CasCap:AIConfig:Providers</c>.</param>
    /// <param name="timeout">Maximum time to wait, including any model load.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The duration of the request.</returns>
    public async Task<TimeSpan> WarmUpAsync(string providerKey, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var provider = aiConfig.Providers[providerKey];
        var agentConfig = new AgentConfig
        {
            Provider = providerKey,
            Name = "warm-up",
            Description = "Loads the model before evaluation.",
            Prompt = string.Empty,
            Instructions = WarmUpPrompt,
        };
        var (chatClient, _, _) = AgentExtensions.CreateAgent(provider, agentConfig,
            tokenCredential: provider.Type is AgentType.AzureOpenAI ? tokenCredential : null);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        var stopwatch = Stopwatch.StartNew();
        await chatClient.GetResponseAsync(WarmUpPrompt, cancellationToken: timeoutCts.Token).ConfigureAwait(false);
        return stopwatch.Elapsed;
    }

    /// <summary>Runs a scenario once and grades the outcome.</summary>
    /// <param name="scenario">The scenario to run.</param>
    /// <param name="providerKey">Key into <c>CasCap:AIConfig:Providers</c> of the model under test.</param>
    /// <param name="instructionVariant">The instruction variant applied to every agent in the tree.</param>
    /// <param name="toolSurfaceVariant">The tool surface variant applied to every agent in the tree.</param>
    /// <param name="repetition">One-based repetition number, for reporting.</param>
    /// <param name="timeout">Maximum duration of the run.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    public async Task<AgentEvaluationRun> RunAsync(
        AgentEvaluationScenario scenario,
        string providerKey,
        InstructionVariant instructionVariant,
        ToolSurfaceVariant toolSurfaceVariant,
        int repetition,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var provider = aiConfig.Providers[providerKey];
        var run = new RunContext(scenario.ToolResponses, providerKey, provider, instructionVariant, toolSurfaceVariant,
            new EvaluationRecorder());

        var services = new ServiceCollection();
        foreach (var agentKey in aiConfig.Agents.Where(a => a.Value.Enabled).Select(a => a.Key))
            services.AddKeyedSingleton(agentKey, (sp, _) => BuildAgent(run, agentKey, sp).Agent);
        var serviceProvider = services.BuildServiceProvider();
        await using var disposal = serviceProvider.ConfigureAwait(false);

        var answer = string.Empty;
        string? error = null;
        var throttlingBefore = throttlingMonitor?.Snapshot() ?? default;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var (agent, agentConfig, instructions) = BuildAgent(run, scenario.AgentKey, serviceProvider);
            var result = await agent.RunAnalysisAsync(
                provider,
                agentConfig,
                AgentExtensions.BuildChatMessage(scenario.Question),
                AgentExtensions.BuildChatOptions(agentConfig, instructions),
                timeout: timeout,
                cancellationToken: cancellationToken,
                logger: loggerFactory.CreateLogger<AgentEvaluationHarness>()).ConfigureAwait(false);
            answer = result.OutputText;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Provider, transport and timeout failures are part of the evaluation result, not caller errors.
            error = $"{ex.GetType().Name}: {ex.Message}";
        }

        stopwatch.Stop();
        var throttlingAfter = throttlingMonitor?.Snapshot() ?? default;

        var toolCalls = run.Recorder.ToolCalls;
        var (answerPassed, toolSelectionPassed, reasons) = AgentEvaluationGrader.Grade(scenario, answer, toolCalls);
        return new AgentEvaluationRun
        {
            ScenarioId = scenario.Id,
            ProviderKey = providerKey,
            ModelName = provider.ModelName,
            InstructionVariant = instructionVariant.Name,
            ToolSurfaceVariant = toolSurfaceVariant.Name,
            Repetition = repetition,
            Answer = answer,
            Error = error,
            Elapsed = stopwatch.Elapsed,
            ToolCalls = toolCalls,
            RoundTrips = run.Recorder.RoundTrips,
            AnswerPassed = answerPassed,
            ToolSelectionPassed = toolSelectionPassed,
            FailureReasons = error is null ? reasons : [$"run failed: {error}", .. reasons],
            ThrottledResponses = throttlingAfter.ThrottledResponses - throttlingBefore.ThrottledResponses,
            Retries = throttlingAfter.Retries - throttlingBefore.Retries,
        };
    }

    /// <summary>
    /// Returns the tools an agent is offered with its configured provider, wrapped for fixtures and recording,
    /// with the tool surface variant applied. Nothing is resolved or invoked.
    /// </summary>
    /// <param name="agentKey">Key into <c>CasCap:AIConfig:Agents</c>.</param>
    /// <param name="toolSurfaceVariant">The variant to apply, or <see langword="null"/> for the shipped surface.</param>
    public IReadOnlyList<FixtureToolFunction> GetOfferedTools(string agentKey, ToolSurfaceVariant? toolSurfaceVariant = null)
    {
        var agentConfig = aiConfig.Agents[agentKey];
        var run = new RunContext(new Dictionary<string, Func<AIFunctionArguments, object?>>(), agentConfig.Provider,
            aiConfig.Providers[agentConfig.Provider], InstructionVariant.Baseline,
            toolSurfaceVariant ?? ToolSurfaceVariant.Baseline, new EvaluationRecorder());
        return BuildTools(run, agentKey, agentConfig, EmptyServiceProvider.Instance,
            loggerFactory.CreateLogger<AgentEvaluationHarness>());
    }

    private (AIAgent Agent, AgentConfig AgentConfig, string Instructions) BuildAgent(RunContext run, string agentKey,
        IServiceProvider serviceProvider)
    {
        var agentConfig = ApplyInstructionVariant(run, agentKey);
        var tools = BuildTools(run, agentKey, agentConfig, serviceProvider, loggerFactory.CreateLogger<AgentEvaluationHarness>());
        var (_, agent, instructions) = AgentExtensions.CreateAgent(
            run.Provider,
            agentConfig,
            tools: tools,
            configureChatClient: builder => ConfigureChatClient(builder, run, agentKey),
            instructionsAssembly: instructionsAssembly,
            aiConfig: aiConfig,
            tokenCredential: run.Provider.Type is AgentType.AzureOpenAI ? tokenCredential : null,
            loggerFactory: loggerFactory);
        return (agent, agentConfig, instructions);
    }

    private AgentConfig ApplyInstructionVariant(RunContext run, string agentKey)
    {
        if (!aiConfig.Agents.TryGetValue(agentKey, out var agentConfig))
            throw new InvalidOperationException($"Agent '{agentKey}' is not defined in CasCap:AIConfig:Agents.");

        var baseline = AgentExtensions.ResolveInstructions(agentConfig, instructionsAssembly);
        return agentConfig with
        {
            Provider = run.ProviderKey,
            Instructions = run.InstructionVariant.Rewrite(agentKey, baseline),
            InstructionsSource = null,
        };
    }

    private List<FixtureToolFunction> BuildTools(RunContext run, string agentKey, AgentConfig agentConfig,
        IServiceProvider serviceProvider, ILogger logger)
    {
        var shipped = new List<AIFunction>(toolCatalog.GetServiceTools(serviceProvider, agentConfig,
            config?.EndpointToolsMapped ?? false, logger));
        foreach (var source in agentConfig.Tools.Where(s => s.Agent is not null))
        {
            var targetKey = source.Agent!;
            if (!aiConfig.Agents.TryGetValue(targetKey, out var target) || !target.Enabled)
                continue;

            var agentTool = AgentExtensions.CreateAgentTool(serviceProvider, targetKey, ApplyInstructionVariant(run, targetKey),
                run.Provider, aiConfig, instructionsAssembly, logger);
            shipped.AddRange(AgentExtensions.FilterTools([agentTool], source, isDevelopment: true, logger).Cast<AIFunction>());
        }

        var variant = run.ToolSurfaceVariant;
        return shipped
            .Select(t => (Tool: t, Disposition: toolCatalog.Classify(t)))
            .Where(t => variant.IsOffered(agentKey, t.Tool.Name, t.Disposition))
            .Concat(variant.GetCandidateTools(agentKey).Select(t => (Tool: t, Disposition: ToolDisposition.Query)))
            .Select(t => new FixtureToolFunction(
                t.Tool,
                agentKey,
                t.Disposition,
                run.ToolResponses.GetValueOrDefault(t.Tool.Name),
                run.Recorder,
                variant.GetDescription(t.Tool.Name),
                variant.RewriteSchema(t.Tool.Name, t.Tool.JsonSchema)))
            .ToList();
    }

    private void ConfigureChatClient(ChatClientBuilder builder, RunContext run, string agentKey)
    {
        if ((config?.ProviderReasoningEffortApplied ?? false) && run.Provider.ReasoningEffort is { } effort)
            builder.ConfigureOptions(options => options.Reasoning ??= new ReasoningOptions { Effort = effort });
        builder.Use(inner => new RecordingChatClient(inner, agentKey, run.Recorder));
    }

    private sealed record RunContext(
        IReadOnlyDictionary<string, Func<AIFunctionArguments, object?>> ToolResponses,
        string ProviderKey,
        ProviderConfig Provider,
        InstructionVariant InstructionVariant,
        ToolSurfaceVariant ToolSurfaceVariant,
        EvaluationRecorder Recorder);
}
