namespace CasCap.Common.Models;

/// <summary>One model's results across every scenario and variant of a test session, for cross-model comparison.</summary>
public sealed record AgentEvaluationProviderSummary
{
    /// <summary>The provider key.</summary>
    public required string ProviderKey { get; init; }

    /// <summary>The model or deployment name.</summary>
    public required string ModelName { get; init; }

    /// <summary>Number of runs.</summary>
    public required int Runs { get; init; }

    /// <summary>Runs that passed every check.</summary>
    public required int Passed { get; init; }

    /// <summary>Runs that selected tools correctly.</summary>
    public required int ToolSelectionsCorrect { get; init; }

    /// <summary>Runs that did not complete.</summary>
    public required int Errors { get; init; }

    /// <summary>Median run duration.</summary>
    public required TimeSpan MedianRun { get; init; }

    /// <summary>90th percentile run duration.</summary>
    public required TimeSpan P90Run { get; init; }

    /// <summary>Median duration of a single model request.</summary>
    public required TimeSpan MedianRequest { get; init; }

    /// <summary>Median duration of a tool-free chat request once the model is loaded, when measured.</summary>
    public TimeSpan? MedianPlainChat { get; init; }

    /// <summary>Mean model requests per run.</summary>
    public required double MeanRequests { get; init; }

    /// <summary>Mean prompt tokens per run, or <see langword="null"/> when none were reported.</summary>
    public double? MeanInputTokens { get; init; }

    /// <summary>Mean generated tokens per run, or <see langword="null"/> when none were reported.</summary>
    public double? MeanOutputTokens { get; init; }

    /// <summary>HTTP 429 responses across all runs.</summary>
    public required long ThrottledResponses { get; init; }

    /// <summary>Runs whose responses contained visible thinking.</summary>
    public required int RunsWithThinking { get; init; }

    /// <summary>
    /// How many times faster than the reference provider, as the geometric mean of median run-time ratios
    /// over the scenario and variant combinations both providers ran; <see langword="null"/> when none are shared.
    /// </summary>
    public double? SpeedupVersusReference { get; init; }

    /// <summary>Fraction of runs that passed every check.</summary>
    public double PassRate => Runs == 0 ? 0d : (double)Passed / Runs;
}
