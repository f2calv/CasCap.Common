namespace CasCap.Models;

/// <summary>MCP-friendly projection of <see cref="AgentConfig"/> (excludes instructions and settings).</summary>
public record AgentInfo
{
    /// <summary>Dictionary key used in <see cref="AIConfig.Agents"/> (matches <see cref="AgentKeys"/> constants).</summary>
    [Description("Unique agent key identifying the agent in AIConfig.Agents.")]
    public required string Key { get; init; }

    /// <summary>Human-readable display name.</summary>
    [Description("Display name of the agent.")]
    public required string Name { get; init; }

    /// <summary>What this agent does.</summary>
    [Description("Brief description of the agent's role and capabilities.")]
    public required string Description { get; init; }

    /// <summary>Whether the agent is currently active.</summary>
    [Description("True when the agent is enabled and accepting requests.")]
    public required bool Enabled { get; init; }

    /// <summary>Key into <see cref="AIConfig.Providers"/> for this agent's backing model.</summary>
    [Description("Provider key referencing AIConfig.Providers.")]
    public required string Provider { get; init; }

    /// <summary>Maximum chat history depth before automatic compaction.</summary>
    [Description("Max non-system messages retained before auto-compaction. Null or 0 = no limit.")]
    public int? MaxMessages { get; init; }

    /// <summary>Number of tool sources configured for this agent.</summary>
    [Description("Count of tool sources (MCP services, remote endpoints, or sub-agent delegations).")]
    public required int ToolSourceCount { get; init; }

    /// <summary>Names of sub-agents this agent can delegate to.</summary>
    [Description("List of agent keys this agent can invoke via tool delegation. Empty if none.")]
    public required string[] DelegatedAgents { get; init; }
}
