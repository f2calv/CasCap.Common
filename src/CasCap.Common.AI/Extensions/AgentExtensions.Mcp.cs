using ModelContextProtocol.Client;

namespace CasCap.Common.Extensions;

/// <summary>MCP client connectivity, prompt discovery, and prompt filtering.</summary>
public static partial class AgentExtensions
{
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
    /// <param name="logger">Optional logger for tool-discovery diagnostics.</param>
    public static async Task<(McpClient Client, List<McpClientTool> Tools)> GetHttpTools(string mcpEndpoint, ILogger? logger = null)
        => await GetHttpTools(mcpEndpoint, additionalHeaders: null, logger).ConfigureAwait(false);

    /// <summary>Creates an authenticated remote MCP connection and retrieves its available tools.</summary>
    /// <param name="mcpEndpoint">The URL of the remote MCP server endpoint.</param>
    /// <param name="additionalHeaders">Optional request headers such as Authorization.</param>
    /// <param name="logger">Optional logger for tool-discovery diagnostics.</param>
    /// <returns>The owned MCP client and discovered tools.</returns>
    public static async Task<(McpClient Client, List<McpClientTool> Tools)> GetHttpTools(
        string mcpEndpoint,
        IReadOnlyDictionary<string, string>? additionalHeaders,
        ILogger? logger = null)
    {
        var mcpClient = await CreateMcpClientAsync(mcpEndpoint, additionalHeaders).ConfigureAwait(false);
        var tools = (await mcpClient.ListToolsAsync().ConfigureAwait(false)).ToList();
        var effectiveLogger = logger ?? NullLogger.Instance;
        foreach (var tool in tools)
            if (effectiveLogger.IsEnabled(LogLevel.Debug))
                effectiveLogger.LogDebug("Discovered remote MCP tool {Name} ({Description})", tool.Name, tool.Description);
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
    /// <param name="logger">Optional logger for prompt-discovery diagnostics.</param>
    public static async Task<(McpClient Client, List<McpClientPrompt> Prompts)> GetHttpPrompts(string mcpEndpoint, ILogger? logger = null)
    {
        var mcpClient = await CreateMcpClientAsync(mcpEndpoint, additionalHeaders: null).ConfigureAwait(false);
        var prompts = (await mcpClient.ListPromptsAsync().ConfigureAwait(false)).ToList();
        var effectiveLogger = logger ?? NullLogger.Instance;
        foreach (var prompt in prompts)
            if (effectiveLogger.IsEnabled(LogLevel.Debug))
                effectiveLogger.LogDebug("Discovered remote MCP prompt {Name} ({Description})", prompt.Name, prompt.Description);
        return (mcpClient, prompts);
    }

    /// <summary>
    /// Creates a new <see cref="McpClient"/> connected to a remote MCP server via Streamable HTTP.
    /// </summary>
    /// <param name="mcpEndpoint">The URL of the remote MCP server endpoint.</param>
    /// <param name="additionalHeaders">Optional headers included in every transport request.</param>
    /// <returns>A connected <see cref="McpClient"/>. The caller owns disposal.</returns>
    private static Task<McpClient> CreateMcpClientAsync(
        string mcpEndpoint,
        IReadOnlyDictionary<string, string>? additionalHeaders) =>
        McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            TransportMode = HttpTransportMode.StreamableHttp,
            Endpoint = new Uri(mcpEndpoint),
            ConnectionTimeout = Timeout.InfiniteTimeSpan,
            Name = $"{Environment.MachineName}-McpClient",
            AdditionalHeaders = additionalHeaders is null
                ? null
                : new Dictionary<string, string>(additionalHeaders, StringComparer.OrdinalIgnoreCase),
        }));

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
    /// <param name="logger">Optional logger for prompt-discovery diagnostics.</param>
    /// <param name="registry">
    /// Optional <see cref="AgentTypeRegistry"/> for deterministic name-to-type resolution. When
    /// <see langword="null"/> every loaded assembly is scanned instead.
    /// </param>
    public static List<McpPromptDescriptor> CreatePromptsForAgent(AgentConfig agentConfig,
        bool isDevelopment = false, ILogger? logger = null, AgentTypeRegistry? registry = null)
    {
        var prompts = new List<McpPromptDescriptor>();

        foreach (var source in agentConfig.Prompts.Where(s => s.Service is not null))
        {
            var promptType = ResolvePromptType(registry, source.Service!, logger);

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

                var effectiveLogger = logger ?? NullLogger.Instance;
                if (effectiveLogger.IsEnabled(LogLevel.Debug))
                    effectiveLogger.LogDebug("Discovered in-process MCP prompt {Name} ({Description})",
                        method.Name, description);
            }

            prompts.AddRange(FilterPrompts(sourcePrompts, source, isDevelopment, logger));
        }

        return prompts;
    }

    /// <summary>
    /// Resolves an MCP prompt type by simple name, preferring the registered
    /// <see cref="AgentTypeRegistry"/> and falling back to an assembly scan when none is present.
    /// </summary>
    private static Type ResolvePromptType(AgentTypeRegistry? registry, string typeName, ILogger? logger)
    {
        if (registry is not null)
        {
            return registry.TryGetPromptType(typeName, out var registered)
                ? registered
                : throw new InvalidOperationException(
                $"Prompt type '{typeName}' is not registered. Known prompt types: "
                + $"{(registry.PromptTypeNames.Count == 0 ? "(none)" : string.Join(", ", registry.PromptTypeNames.Order()))}. "
                + "Pass the declaring assembly to AddAgentTypeRegistry().");
        }

        var effectiveLogger = logger ?? NullLogger.Instance;
        if (effectiveLogger.IsEnabled(LogLevel.Warning))
            effectiveLogger.LogWarning(
                "No {RegistryType} registered — falling back to scanning every loaded assembly for prompt type '{TypeName}'. "
                + "Call services.AddAgentTypeRegistry(assembly) to make resolution deterministic.",
                nameof(AgentTypeRegistry), typeName);

        return ScanForType(typeName, "Prompt",
            t => t.GetCustomAttribute<McpServerPromptTypeAttribute>() is not null);
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
    /// <param name="logger">Optional logger for filter diagnostics.</param>
    public static IEnumerable<McpPromptDescriptor> FilterPrompts(
        IEnumerable<McpPromptDescriptor> prompts, PromptSource source, bool isDevelopment = false, ILogger? logger = null)
    {
        var promptList = prompts.ToList();
        var availableNames = promptList.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var misconfigured = new List<string>();

        if (source.IncludePrompts.Length > 0)
        {
            foreach (var name in source.IncludePrompts)
                if (!availableNames.Contains(name))
                    misconfigured.Add($"included prompt '{name}' not found");
            promptList = [.. promptList.Where(p => source.IncludePrompts.Contains(p.Name, StringComparer.OrdinalIgnoreCase))];
        }

        if (source.ExcludePrompts.Length > 0)
        {
            foreach (var name in source.ExcludePrompts)
                if (!availableNames.Contains(name))
                    misconfigured.Add($"excluded prompt '{name}' not found");
            promptList = [.. promptList.Where(p => !source.ExcludePrompts.Contains(p.Name, StringComparer.OrdinalIgnoreCase))];
        }

        ReportMisconfigured(misconfigured, "prompts", isDevelopment, logger);

        var effectiveLogger = logger ?? NullLogger.Instance;
        foreach (var prompt in promptList)
            if (effectiveLogger.IsEnabled(LogLevel.Debug))
                effectiveLogger.LogDebug("Enabled prompt {PromptName}", prompt.Name);

        return promptList;
    }

    /// <summary>
    /// Converts a sequence of remote <see cref="McpClientPrompt"/> instances to
    /// <see cref="McpPromptDescriptor"/> instances for unified prompt handling.
    /// </summary>
    /// <param name="mcpPrompts">The remote prompts returned by <c>ListPromptsAsync</c>.</param>
    /// <returns>A list of <see cref="McpPromptDescriptor"/> mapped from the remote prompts.</returns>
    public static List<McpPromptDescriptor> ToPromptDescriptors(this IEnumerable<McpClientPrompt> mcpPrompts) =>
        [.. mcpPrompts.Select(p => new McpPromptDescriptor
        {
            Name = p.Name,
            Description = p.Description,
            Parameters = [.. (p.ProtocolPrompt.Arguments ?? []).Select(a => new McpPromptDescriptor.Parameter
            {
                Name = a.Name,
                Description = a.Description,
                Required = a.Required ?? false,
            })],
        })];
}
