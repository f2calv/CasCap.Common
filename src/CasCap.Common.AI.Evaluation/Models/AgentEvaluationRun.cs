namespace CasCap.Common.Models;

/// <summary>The outcome of one scenario run against one model, instruction variant and tool surface variant.</summary>
public sealed record AgentEvaluationRun
{
    /// <summary>The scenario identifier.</summary>
    public required string ScenarioId { get; init; }

    /// <summary>The provider key from <c>CasCap:AIConfig:Providers</c>.</summary>
    public required string ProviderKey { get; init; }

    /// <summary>The model or deployment name.</summary>
    public required string ModelName { get; init; }

    /// <summary>The instruction variant name.</summary>
    public required string InstructionVariant { get; init; }

    /// <summary>The tool surface variant name.</summary>
    public required string ToolSurfaceVariant { get; init; }

    /// <summary>One-based repetition number.</summary>
    public required int Repetition { get; init; }

    /// <summary>The final answer text, or empty when the run failed.</summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>The exception type and message when the run did not complete, for example a timeout or HTTP error.</summary>
    public string? Error { get; init; }

    /// <summary>Wall-clock duration of the whole run, including delegation.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Tool calls at every delegation depth.</summary>
    public IReadOnlyList<RecordedToolCall> ToolCalls { get; init; } = [];

    /// <summary>Model requests at every delegation depth.</summary>
    public IReadOnlyList<ModelRoundTrip> RoundTrips { get; init; } = [];

    /// <summary>Whether the answer satisfied the scenario's <see cref="AgentEvaluationScenario.Answer"/>.</summary>
    public bool AnswerPassed { get; init; }

    /// <summary>Whether the required tools were called and no forbidden or side-effect tool was.</summary>
    public bool ToolSelectionPassed { get; init; }

    /// <summary>Reasons the run failed, empty when it passed.</summary>
    public IReadOnlyList<string> FailureReasons { get; init; } = [];

    /// <summary>HTTP 429 responses the Azure SDK received during the run, before retrying.</summary>
    public long ThrottledResponses { get; init; }

    /// <summary>Request retries the Azure SDK made during the run, for throttling or transient failures.</summary>
    public long Retries { get; init; }

    /// <summary>Characters of visible thinking across every model request of the run.</summary>
    public int ReasoningCharacters => RoundTrips.Sum(r => r.ReasoningCharacters);

    /// <summary>Whether the run completed, answered correctly and selected tools correctly.</summary>
    public bool Passed => Error is null && AnswerPassed && ToolSelectionPassed;

    /// <summary>Prompt tokens summed across every model request, or <see langword="null"/> when none were reported.</summary>
    public long? InputTokens => RoundTrips.Any(r => r.InputTokens is not null) ? RoundTrips.Sum(r => r.InputTokens ?? 0) : null;

    /// <summary>Generated tokens summed across every model request, or <see langword="null"/> when none were reported.</summary>
    public long? OutputTokens => RoundTrips.Any(r => r.OutputTokens is not null) ? RoundTrips.Sum(r => r.OutputTokens ?? 0) : null;
}
