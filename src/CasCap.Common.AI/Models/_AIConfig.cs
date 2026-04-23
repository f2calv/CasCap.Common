namespace CasCap.Models;

/// <summary>
/// AI agent configuration: named providers and agents keyed by identifier.
/// </summary>
public record AIConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(AIConfig)}";

    /// <summary>
    /// Named infrastructure providers. Each provider defines a connection type, endpoint and model.
    /// </summary>
    [Required, MinLength(1)]
    public required Dictionary<string, ProviderConfig> Providers { get; init; } = [];

    /// <summary>
    /// Named AI agents. Each agent references an <see cref="ProviderConfig"/> and defines instructions, prompt and behavior.
    /// </summary>
    [Required, MinLength(1)]
    public required Dictionary<string, AgentConfig> Agents { get; init; } = [];

    /// <summary>
    /// The URL path for the MCP server endpoint. Defaults to <c>"/mcp"</c>.
    /// </summary>
    public string McpUrl { get; init; } = "/mcp";

    /// <summary>
    /// Text prepended to every agent's resolved instructions. Use for shared preamble or identity rules
    /// that apply to all agents.
    /// </summary>
    /// <remarks>Defaults to <see cref="string.Empty"/> (no prefix).</remarks>
    public string InstructionsPrefix { get; init; } = string.Empty;

    /// <summary>
    /// Text appended to every agent's resolved instructions. Use for shared behavioral rules
    /// (e.g. poll rules, safety constraints) that apply to all agents.
    /// </summary>
    /// <remarks>Defaults to <see cref="string.Empty"/> (no suffix).</remarks>
    public string InstructionsSuffix { get; init; } = string.Empty;

    /// <summary>
    /// IANA time zone identifier for the house location.
    /// </summary>
    /// <remarks>Defaults to <c>Europe/Berlin</c>.</remarks>
    public string TimeZoneId { get; init; } = "Europe/Berlin";

    /// <summary>
    /// Time-to-live in milliseconds for in-memory polls before automatic expiry.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>3600000</c> ms (1 hour).
    /// Used by <see cref="CasCap.Services.InMemoryPollTracker"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int PollTtlMs { get; init; } = 3_600_000;

    /// <summary>
    /// Sliding expiration in days applied to persisted agent sessions.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>7</c> days.
    /// Used by <see cref="CasCap.Services.AgentCommandHandler"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int SessionTtlDays { get; init; } = 7;
}
