using System.Text.Json.Nodes;

namespace CasCap.Common.Models;

/// <summary>
/// A named change to the tools an agent is offered: rewritten tool or parameter descriptions, hidden tools,
/// a reduced tool set, or candidate tools that do not exist yet.
/// </summary>
/// <remarks>
/// Variants never change a tool's behaviour. They exist to test whether the model-facing surface — the
/// names, descriptions and JSON schemas sent with every request — leads a model to the right tool, and
/// whether a smaller surface helps a small language model.
/// </remarks>
public sealed record ToolSurfaceVariant
{
    /// <summary>The variant evaluated as the reference point for every scenario.</summary>
    public static ToolSurfaceVariant Baseline { get; } = new() { Name = "baseline", Notes = "Tools as shipped." };

    /// <summary>Short, unique name used in reports and in <see cref="AgentEvaluationConfig.ToolSurfaceVariants"/>.</summary>
    public required string Name { get; init; }

    /// <summary>What the variant changes and why.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Replacement descriptions keyed by model-facing tool name, for example <c>get_order_status</c>.</summary>
    public IReadOnlyDictionary<string, string> DescriptionOverrides { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Replacement parameter descriptions keyed by <c>tool_name.parameterName</c>, for example
    /// <c>get_order_status.orderId</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> ParameterDescriptionOverrides { get; init; } = new Dictionary<string, string>();

    /// <summary>Model-facing tool names removed from every agent.</summary>
    public IReadOnlyList<string> HiddenTools { get; init; } = [];

    /// <summary>
    /// Per-agent allowlists keyed by agent key. When an agent has an entry, only the listed tools, its
    /// candidate tools and its delegation tools are offered.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> ToolAllowLists { get; init; } = new Dictionary<string, string[]>();

    /// <summary>
    /// Proposed tools keyed by agent key. Create them with <see cref="AIFunctionFactory"/>; the scenario
    /// fixture supplies their responses, so a tool can be evaluated before it is implemented.
    /// </summary>
    public IReadOnlyDictionary<string, AIFunction[]> CandidateTools { get; init; } = new Dictionary<string, AIFunction[]>();

    /// <summary>Returns whether a tool from the shipped surface is offered to an agent.</summary>
    /// <param name="agentKey">The agent being built.</param>
    /// <param name="toolName">The model-facing tool name.</param>
    /// <param name="disposition">How the harness classifies the tool.</param>
    public bool IsOffered(string agentKey, string toolName, ToolDisposition disposition)
    {
        if (HiddenTools.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            return false;
        if (disposition is ToolDisposition.Delegation || !ToolAllowLists.TryGetValue(agentKey, out var allowList))
            return true;
        return allowList.Contains(toolName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns the proposed tools for an agent.</summary>
    /// <param name="agentKey">The agent being built.</param>
    public IReadOnlyList<AIFunction> GetCandidateTools(string agentKey) =>
        CandidateTools.TryGetValue(agentKey, out var tools) ? tools : [];

    /// <summary>Returns the description override for a tool, or <see langword="null"/> to keep the shipped one.</summary>
    /// <param name="toolName">The model-facing tool name.</param>
    public string? GetDescription(string toolName) =>
        DescriptionOverrides.TryGetValue(toolName, out var description) ? description : null;

    /// <summary>
    /// Returns the tool's parameter schema with any <see cref="ParameterDescriptionOverrides"/> applied,
    /// or <see langword="null"/> when the variant leaves the schema unchanged.
    /// </summary>
    /// <param name="toolName">The model-facing tool name.</param>
    /// <param name="schema">The shipped JSON schema.</param>
    /// <exception cref="InvalidOperationException">An override names a parameter the tool does not have.</exception>
    public JsonElement? RewriteSchema(string toolName, JsonElement schema)
    {
        var prefix = $"{toolName}.";
        var overrides = ParameterDescriptionOverrides
            .Where(o => o.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (overrides.Count == 0)
            return null;

        var root = JsonNode.Parse(schema.GetRawText())!.AsObject();
        var properties = root["properties"]?.AsObject()
            ?? throw new InvalidOperationException($"Tool '{toolName}' has no parameters to describe.");
        foreach (var (key, description) in overrides)
        {
            var parameter = key[prefix.Length..];
            var property = properties[parameter]?.AsObject()
                ?? throw new InvalidOperationException($"Tool '{toolName}' has no parameter '{parameter}'.");
            property["description"] = description;
        }

        return JsonSerializer.SerializeToElement(root);
    }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
