namespace CasCap.Models;

/// <summary>
/// Identifies a single source of MCP prompts for an agent, with optional include/exclude
/// filters for cherry-picking individual prompts from the source.
/// Exactly one of <see cref="Service"/> or <see cref="Endpoint"/> must be set.
/// </summary>
public record PromptSource : IValidatableObject
{
    /// <summary>
    /// Simple class name of a <c>*Prompts</c> type decorated with
    /// <c>[McpServerPromptType]</c> whose <c>[McpServerPrompt]</c>-decorated methods
    /// should be loaded as in-process prompts (e.g. <c>"HeatPumpPrompts"</c>).
    /// Mutually exclusive with <see cref="Endpoint"/>.
    /// </summary>
    public string? Service { get; init; }

    /// <summary>
    /// Remote MCP server URL whose prompts are fetched at startup via Streamable HTTP.
    /// Mutually exclusive with <see cref="Service"/>.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// When non-empty, only prompts whose names appear in this list are loaded from the source.
    /// An empty array means all prompts are included (subject to <see cref="ExcludePrompts"/>).
    /// </summary>
    public string[] IncludePrompts { get; init; } = [];

    /// <summary>
    /// Prompt names to exclude from this source. Applied after <see cref="IncludePrompts"/>,
    /// allowing an "all except…" pattern when <see cref="IncludePrompts"/> is empty.
    /// </summary>
    public string[] ExcludePrompts { get; init; } = [];

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Service) && string.IsNullOrWhiteSpace(Endpoint))
            yield return new ValidationResult(
                $"Either {nameof(Service)} or {nameof(Endpoint)} must be set.",
                [nameof(Service), nameof(Endpoint)]);

        if (!string.IsNullOrWhiteSpace(Service) && !string.IsNullOrWhiteSpace(Endpoint))
            yield return new ValidationResult(
                $"{nameof(Service)} and {nameof(Endpoint)} are mutually exclusive.",
                [nameof(Service), nameof(Endpoint)]);
    }
}
