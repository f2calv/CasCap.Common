namespace CasCap.Common.Models;

/// <summary>Aggregated outcome of every repetition of one scenario, model and variant combination.</summary>
public sealed record AgentEvaluationSummary
{
    /// <summary>The scenario identifier.</summary>
    public required string ScenarioId { get; init; }

    /// <summary>The provider key.</summary>
    public required string ProviderKey { get; init; }

    /// <summary>The model or deployment name.</summary>
    public required string ModelName { get; init; }

    /// <summary>The instruction variant name.</summary>
    public required string InstructionVariant { get; init; }

    /// <summary>The tool surface variant name.</summary>
    public required string ToolSurfaceVariant { get; init; }

    /// <summary>Number of runs.</summary>
    public required int Runs { get; init; }

    /// <summary>Runs that passed every check.</summary>
    public required int Passed { get; init; }

    /// <summary>Runs whose answer was correct, regardless of tool selection.</summary>
    public required int AnswersCorrect { get; init; }

    /// <summary>Runs whose tool selection was correct, regardless of the answer.</summary>
    public required int ToolSelectionsCorrect { get; init; }

    /// <summary>Runs that did not complete, for example because of a timeout or provider error.</summary>
    public required int Errors { get; init; }

    /// <summary>HTTP 429 responses the Azure SDK received across the runs.</summary>
    public required long ThrottledResponses { get; init; }

    /// <summary>Runs whose responses contained visible thinking.</summary>
    public required int RunsWithThinking { get; init; }

    /// <summary>Lower bound of the 95% Wilson score interval for the pass rate.</summary>
    public required double PassRateLower { get; init; }

    /// <summary>Upper bound of the 95% Wilson score interval for the pass rate.</summary>
    public required double PassRateUpper { get; init; }

    /// <summary>Median run duration.</summary>
    public required TimeSpan MedianElapsed { get; init; }

    /// <summary>Longest run duration.</summary>
    public required TimeSpan MaxElapsed { get; init; }

    /// <summary>Mean model requests per run, across every agent in the tree.</summary>
    public required double MeanRoundTrips { get; init; }

    /// <summary>Mean tool calls per run, across every agent in the tree.</summary>
    public required double MeanToolCalls { get; init; }

    /// <summary>Mean prompt tokens per run, or <see langword="null"/> when the provider reported none.</summary>
    public double? MeanInputTokens { get; init; }

    /// <summary>Mean generated tokens per run, or <see langword="null"/> when the provider reported none.</summary>
    public double? MeanOutputTokens { get; init; }

    /// <summary>The most frequent failure reasons with their counts.</summary>
    public IReadOnlyList<string> TopFailureReasons { get; init; } = [];

    /// <summary>Fraction of runs that passed every check.</summary>
    public double PassRate => Runs == 0 ? 0d : (double)Passed / Runs;
}
