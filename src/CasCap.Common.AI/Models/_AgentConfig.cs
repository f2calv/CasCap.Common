namespace CasCap.Models;

/// <summary>
/// Behavioral configuration for a named AI agent: instructions, prompt, reasoning, tools and prompts.
/// </summary>
/// <remarks>
/// Each <see cref="ToolSource"/> in <see cref="Tools"/> identifies a service class or remote
/// MCP endpoint and optionally filters to a subset of the available tools.
/// Prompts follow the same dual-source pattern via <see cref="PromptSource"/> in <see cref="Prompts"/>.
/// References a <see cref="ProviderConfig"/> by key via <see cref="Provider"/>.
/// Multiple agents are defined in <see cref="AIConfig.Agents"/> keyed by <see cref="AgentKeys"/> constants.
/// Agent-specific orchestration settings live in the <see cref="Settings"/> sub-section, bound
/// to a dedicated strongly-typed record.
/// </remarks>
public record AgentConfig
{
    /// <summary>Whether this agent is active. Disabled agents are not registered in DI and are skipped during sub-agent resolution.</summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Key into <see cref="AIConfig.Providers"/> identifying the infrastructure provider.
    /// </summary>
    [Required, MinLength(1)]
    public required string Provider { get; init; }

    /// <summary>
    /// System instructions for the agent.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Optional instruction source: an embedded resource name or an absolute file system path.
    /// </summary>
    /// <remarks>Overrides <see cref="Instructions"/> when set. Resolved by first checking embedded resources, then the file system.</remarks>
    public string? InstructionsSource { get; init; }

    /// <summary>
    /// Display description for the agent.
    /// </summary>
    [Required, MinLength(1)]
    public required string Description { get; init; }

    /// <summary>Display name for the agent.</summary>
    /// <remarks>Also used to derive the Redis session key (<c>agents:sessions:{name}:active</c>).</remarks>
    [Required, MinLength(1)]
    public required string Name { get; init; }

    /// <summary>
    /// The default prompt sent automatically with image/event data.
    /// </summary>
    [Required, MinLength(1)]
    public required string Prompt { get; init; }

    /// <summary>
    /// Maximum number of non-system messages to retain in the chat history before
    /// automatic compaction strips older messages and tool output.
    /// </summary>
    /// <remarks>
    /// When set to a positive value, the agent's <see cref="Microsoft.Agents.AI.InMemoryChatHistoryProvider"/>
    /// is configured with a <see cref="CasCap.Services.ToolOutputStrippingChatReducer"/> that drops
    /// <see cref="Microsoft.Extensions.AI.FunctionCallContent"/> /
    /// <see cref="Microsoft.Extensions.AI.FunctionResultContent"/> messages and keeps only the newest
    /// <see cref="MaxMessages"/> exchanges. When <see langword="null"/> or zero, no automatic
    /// compaction is applied.
    /// Defaults to <c>20</c>.
    /// </remarks>
    [Range(0, int.MaxValue)]
    public int? MaxMessages { get; init; } = 20;

    /// <summary>
    /// Tool sources for this agent. Each <see cref="ToolSource"/> identifies either an
    /// in-process service class or a remote MCP endpoint, with optional include/exclude
    /// filters for cherry-picking individual tools across multiple sources.
    /// </summary>
    public ToolSource[] Tools { get; init; } = [];

    /// <summary>
    /// Prompt sources for this agent. Each <see cref="PromptSource"/> identifies either an
    /// in-process prompt type or a remote MCP endpoint, with optional include/exclude
    /// filters for cherry-picking individual prompts across multiple sources.
    /// </summary>
    public PromptSource[] Prompts { get; init; } = [];

    /// <summary>
    /// Agent-specific orchestration settings (<see langword="object"/> placeholder for the configuration binder).
    /// </summary>
    /// <remarks>
    /// The JSON sub-tree under this property is bound to a dedicated <see cref="IAppConfig"/>
    /// record via
    /// <c>BindConfiguration($"{nameof(AgentKeys.AIConfig)}:{nameof(AgentKeys.Agents)}:{AgentKeys.Xxx}:{nameof(AgentKeys.Settings)}")</c>.
    /// Consumers should never read this property directly — inject the strongly-typed
    /// <see cref="IOptions{T}"/> record instead.
    /// </remarks>
    public object? Settings { get; init; }
}
