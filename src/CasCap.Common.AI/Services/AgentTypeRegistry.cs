namespace CasCap.Common.Services;

/// <summary>
/// Deterministic name-to-type lookup for agent tool services and MCP prompt types, replacing
/// the previous scan of every type in every loaded assembly.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AgentConfig.Tools"/> and <see cref="AgentConfig.Prompts"/> reference types by
/// simple name (e.g. <c>"InverterMcpQueryService"</c>). Resolving those by walking
/// <see cref="AppDomain.GetAssemblies"/> is dependent on assembly load order, silently ambiguous
/// when two assemblies expose the same simple name, linear in the total type count, and hostile
/// to trimming. This registry is built once at startup from the service collection and the
/// explicitly supplied prompt assemblies, so lookups are exact and O(1).
/// </para>
/// <para>Register via <c>services.AddAgentTypeRegistry(...)</c>.</para>
/// </remarks>
public sealed class AgentTypeRegistry
{
    private readonly FrozenDictionary<string, Type> _toolTypes;
    private readonly FrozenDictionary<string, Type> _promptTypes;

    /// <summary>Initializes a new instance of the <see cref="AgentTypeRegistry"/> class.</summary>
    /// <param name="toolTypes">Types exposing <see cref="McpServerToolAttribute"/>-decorated methods.</param>
    /// <param name="promptTypes">Types decorated with <see cref="McpServerPromptTypeAttribute"/>.</param>
    /// <exception cref="InvalidOperationException">Two distinct types share a simple name.</exception>
    public AgentTypeRegistry(IEnumerable<Type> toolTypes, IEnumerable<Type> promptTypes)
    {
        _toolTypes = BuildIndex(toolTypes, "tool service");
        _promptTypes = BuildIndex(promptTypes, "prompt");
    }

    private static FrozenDictionary<string, Type> BuildIndex(IEnumerable<Type> types, string label)
    {
        var index = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var type in types.Distinct())
        {
            if (index.TryGetValue(type.Name, out var existing))
            {
                if (existing == type)
                    continue;

                // Silently picking one of these is how the old assembly scan produced
                // deployment-specific behaviour that only showed up at runtime.
                throw new InvalidOperationException(
                    $"Ambiguous {label} name '{type.Name}': both '{existing.FullName}' and '{type.FullName}' match. "
                    + "Agent configuration references these types by simple name, so the names must be unique.");
            }

            index[type.Name] = type;
        }

        return index.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Simple names of every registered tool service type.</summary>
    public IReadOnlyCollection<string> ToolTypeNames => _toolTypes.Keys;

    /// <summary>Simple names of every registered prompt type.</summary>
    public IReadOnlyCollection<string> PromptTypeNames => _promptTypes.Keys;

    /// <summary>Attempts to resolve a tool service type by simple name.</summary>
    /// <param name="name">The simple type name from <see cref="ToolSource.Service"/>.</param>
    /// <param name="type">The resolved type when <see langword="true"/> is returned.</param>
    public bool TryGetToolType(string name, [NotNullWhen(true)] out Type? type) =>
        _toolTypes.TryGetValue(name, out type);

    /// <summary>Attempts to resolve an MCP prompt type by simple name.</summary>
    /// <param name="name">The simple type name from <see cref="PromptSource.Service"/>.</param>
    /// <param name="type">The resolved type when <see langword="true"/> is returned.</param>
    public bool TryGetPromptType(string name, [NotNullWhen(true)] out Type? type) =>
        _promptTypes.TryGetValue(name, out type);
}
