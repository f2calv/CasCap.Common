namespace CasCap.Common.Models;

/// <summary>A tool call made by the model during an evaluation run.</summary>
/// <param name="AgentKey">The agent whose model made the call.</param>
/// <param name="ToolName">The model-facing tool name.</param>
/// <param name="Disposition">How the harness treated the call.</param>
/// <param name="ArgumentsJson">The model-supplied arguments as JSON.</param>
/// <param name="FixtureFound">Whether the scenario supplied a response; <see langword="false"/> marks an unanticipated path.</param>
public sealed record RecordedToolCall(
    string AgentKey,
    string ToolName,
    ToolDisposition Disposition,
    string ArgumentsJson,
    bool FixtureFound);
