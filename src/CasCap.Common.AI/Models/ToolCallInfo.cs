namespace CasCap.Models;

/// <summary>Captures the name and arguments of a single tool/function call during an agent run.</summary>
public sealed record ToolCallInfo(string Name, IDictionary<string, object?>? Arguments);
