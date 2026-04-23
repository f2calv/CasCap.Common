namespace CasCap.Models;

/// <summary>
/// Identifies a single source of MCP tools for an agent, with optional include/exclude
/// filters for cherry-picking individual tools from the source.
/// Exactly one of <see cref="Service"/>, <see cref="Endpoint"/>, or <see cref="Agent"/> must be set.
/// </summary>
public record ToolSource : IValidatableObject
{
    /// <summary>
    /// Simple class name of a <c>*McpQueryService</c> type whose
    /// <see cref="McpServerToolAttribute"/>-decorated methods should be loaded
    /// as in-process tools (e.g. <c>"InverterMcpQueryService"</c>).
    /// Mutually exclusive with <see cref="Endpoint"/> and <see cref="Agent"/>.
    /// </summary>
    public string? Service { get; init; }

    /// <summary>
    /// Remote MCP server URL whose tools are fetched at startup via Streamable HTTP.
    /// Mutually exclusive with <see cref="Service"/> and <see cref="Agent"/>.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Key of another <see cref="AgentConfig"/> in <see cref="AIConfig.Agents"/> that should be
    /// exposed as a single callable tool on this agent (fan-out / agent delegation pattern).
    /// </summary>
    /// <remarks>
    /// When set, a single <see cref="Microsoft.Extensions.AI.AITool"/> named
    /// <c>invoke_{agent_key_snake_case}</c> is created that accepts a text task, runs the
    /// target agent, and returns its output text. The target agent is resolved lazily from the
    /// DI container via its keyed singleton registration. Mutually exclusive with
    /// <see cref="Service"/> and <see cref="Endpoint"/>.
    /// </remarks>
    public string? Agent { get; init; }

    /// <summary>
    /// When non-empty, only tools whose names appear in this list are loaded from the source.
    /// An empty array means all tools are included (subject to <see cref="ExcludeTools"/>).
    /// Tool names use the snake_case convention produced by the tool factory.
    /// </summary>
    public string[] IncludeTools { get; init; } = [];

    /// <summary>
    /// Tool names to exclude from this source. Applied after <see cref="IncludeTools"/>,
    /// allowing an "all except…" pattern when <see cref="IncludeTools"/> is empty.
    /// Tool names use the snake_case convention produced by the tool factory.
    /// </summary>
    public string[] ExcludeTools { get; init; } = [];

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var setCount = (string.IsNullOrWhiteSpace(Service) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(Endpoint) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(Agent) ? 0 : 1);

        if (setCount == 0)
            yield return new ValidationResult(
                $"Exactly one of {nameof(Service)}, {nameof(Endpoint)}, or {nameof(Agent)} must be set.",
                [nameof(Service), nameof(Endpoint), nameof(Agent)]);

        if (setCount > 1)
            yield return new ValidationResult(
                $"{nameof(Service)}, {nameof(Endpoint)}, and {nameof(Agent)} are mutually exclusive.",
                [nameof(Service), nameof(Endpoint), nameof(Agent)]);
    }
}
