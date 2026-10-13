namespace CasCap.Common.Models;

/// <summary>Captures the name and arguments of a single tool/function call during an agent run.</summary>
/// <param name="Name">The tool name.</param>
/// <param name="Arguments">The arguments the model supplied.</param>
/// <param name="Failed">Whether the tool threw or returned an error result.</param>
public sealed record ToolCallInfo(string Name, IDictionary<string, object?>? Arguments, bool Failed = false);
