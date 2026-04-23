namespace CasCap.Models;

/// <summary>
/// Lightweight descriptor for an MCP prompt discovered from either a remote
/// MCP server (via <c>ListPromptsAsync</c>) or an in-process type decorated
/// with <c>[McpServerPromptType]</c>.
/// </summary>
public record McpPromptDescriptor
{
    /// <summary>
    /// Display name of the prompt.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of what the prompt does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Parameter definitions accepted by the prompt.
    /// </summary>
    public IReadOnlyList<Parameter> Parameters { get; init; } = [];

    /// <summary>
    /// Describes a single parameter accepted by an MCP prompt.
    /// </summary>
    public record Parameter
    {
        /// <summary>
        /// Parameter name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Human-readable description of the parameter.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Whether the parameter is required. When <see langword="false"/> the prompt
        /// provides a default value.
        /// </summary>
        public bool Required { get; init; }
    }
}
