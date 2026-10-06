namespace CasCap.Common.Services;

/// <summary>
/// Builds an agent's production tool surface from its <see cref="AgentConfig.Tools"/> sources, without
/// resolving or starting any of the services behind the tools.
/// </summary>
/// <remarks>
/// Service sources use the same <see cref="AgentExtensions.CreateToolsFromServiceProvider(IServiceProvider, Type, bool)"/>
/// and <see cref="AgentExtensions.FilterTools"/> path as <see cref="AgentExtensions.CreateToolsForAgent"/>, with
/// deferred resolution so no backing service is needed. Endpoint sources are ignored by
/// <see cref="AgentExtensions.CreateToolsForAgent"/>; when requested they are mapped onto the in-process tools
/// of the same names, which assumes the remote MCP endpoint serves the same tool classes.
/// </remarks>
public sealed class McpToolCatalog
{
    private readonly Dictionary<string, Type> _toolTypes;
    private readonly IReadOnlyList<string> _queryPrefixes;

    /// <summary>Initializes the catalog from explicit MCP tool types.</summary>
    /// <param name="toolTypes">Types whose <see cref="McpServerToolAttribute"/> methods become tools.</param>
    /// <param name="queryPrefixes">
    /// Tool-name prefixes treated as read-only when a tool is not annotated
    /// <see cref="McpServerToolAttribute.ReadOnly"/>; defaults to <see cref="DefaultQueryPrefixes"/>.
    /// </param>
    /// <exception cref="ArgumentException">Two tool types share a simple name.</exception>
    public McpToolCatalog(IEnumerable<Type> toolTypes, IEnumerable<string>? queryPrefixes = null)
    {
        _toolTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in toolTypes)
            if (!_toolTypes.TryAdd(type.Name, type) && _toolTypes[type.Name] != type)
                throw new ArgumentException($"Ambiguous tool type name '{type.Name}'.", nameof(toolTypes));
        _queryPrefixes = queryPrefixes?.ToList() ?? DefaultQueryPrefixes;
    }

    /// <summary>Read-only tool-name prefixes used when a tool carries no <see cref="McpServerToolAttribute.ReadOnly"/> annotation.</summary>
    public static IReadOnlyList<string> DefaultQueryPrefixes { get; } = ["get_", "list_", "search_", "test_", "validate_"];

    /// <summary>Simple names of every MCP tool type in the catalog.</summary>
    public IReadOnlyCollection<string> ToolTypeNames => _toolTypes.Keys;

    /// <summary>Creates a catalog of every <see cref="McpServerToolTypeAttribute"/> type in the given assemblies.</summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    public static McpToolCatalog FromAssemblies(params Assembly[] assemblies) =>
        new(assemblies.SelectMany(a => a.GetTypes()).Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null));

    /// <summary>
    /// Classifies a tool: delegation tools are named <c>invoke_*</c>; a tool is a query when it is annotated
    /// <see cref="McpServerToolAttribute.ReadOnly"/> or its name starts with a query prefix; anything else is
    /// treated as a side effect.
    /// </summary>
    /// <param name="tool">The tool to classify.</param>
    public ToolDisposition Classify(AIFunction tool)
        => tool.Name.StartsWith("invoke_", StringComparison.Ordinal)
            ? ToolDisposition.Delegation
            : tool.UnderlyingMethod?.GetCustomAttribute<McpServerToolAttribute>()?.ReadOnly == true
                || _queryPrefixes.Any(p => tool.Name.StartsWith(p, StringComparison.Ordinal))
            ? ToolDisposition.Query
            : ToolDisposition.SideEffect;

    /// <summary>Returns every in-process tool across all catalogued types.</summary>
    /// <param name="serviceProvider">Resolves backing services only if a tool is invoked unwrapped; defaults to none.</param>
    public IEnumerable<AIFunction> GetAllTools(IServiceProvider? serviceProvider = null) =>
        _toolTypes.Values.SelectMany(t => CreateTools(serviceProvider ?? EmptyServiceProvider.Instance, t));

    /// <summary>
    /// Returns the Service tools of an agent after include and exclude filtering, as
    /// <see cref="AgentExtensions.CreateToolsForAgent"/> offers them, optionally followed by its Endpoint tools.
    /// Agent (delegation) sources are not included.
    /// </summary>
    /// <param name="serviceProvider">Resolves backing services only if a tool is invoked unwrapped.</param>
    /// <param name="agentConfig">The agent whose tool sources are resolved.</param>
    /// <param name="endpointToolsMapped">See <see cref="AgentEvaluationConfig.EndpointToolsMapped"/>.</param>
    /// <param name="logger">Receives tool filter diagnostics.</param>
    /// <exception cref="InvalidOperationException">A source names an unknown service or a missing tool.</exception>
    public List<AIFunction> GetServiceTools(IServiceProvider serviceProvider, AgentConfig agentConfig,
        bool endpointToolsMapped = false, ILogger? logger = null)
    {
        var tools = new List<AIFunction>();
        foreach (var source in agentConfig.Tools.Where(s => s.Service is not null))
        {
            if (!_toolTypes.TryGetValue(source.Service!, out var type))
                throw new InvalidOperationException(
                    $"Agent '{agentConfig.Name}' references unknown tool service '{source.Service}'.");
            tools.AddRange(AgentExtensions.FilterTools(CreateTools(serviceProvider, type), source, isDevelopment: true, logger)
                .Cast<AIFunction>());
        }

        if (endpointToolsMapped)
            foreach (var source in agentConfig.Tools.Where(s => s.Endpoint is not null))
                tools.AddRange(AgentExtensions.FilterTools(GetAllTools(serviceProvider), source, isDevelopment: true, logger)
                    .Cast<AIFunction>());

        return [.. tools
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .Select(g => g.First())];
    }

    private static IEnumerable<AIFunction> CreateTools(IServiceProvider serviceProvider, Type type) =>
        AgentExtensions.CreateToolsFromServiceProvider(serviceProvider, type, deferResolution: true).Cast<AIFunction>();
}
