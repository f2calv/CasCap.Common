using System.ComponentModel.DataAnnotations;

namespace CasCap.Common.Models;

/// <summary>
/// Selects the models, variants, repetitions and pass threshold for an agent evaluation.
/// </summary>
/// <remarks>
/// Bound from <c>CasCap:AgentEvaluationConfig</c>. Every property has a default, so the section is
/// optional; keep the model matrix, whose endpoints are usually private, in gitignored local configuration.
/// </remarks>
public sealed record AgentEvaluationConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(AgentEvaluationConfig)}";


    /// <summary>
    /// Keys into <c>CasCap:AIConfig:Providers</c> identifying the models to evaluate, for example an edge
    /// small language model and one or more Azure OpenAI deployments.
    /// </summary>
    /// <remarks>
    /// Defaults to empty, leaving the consumer to choose a fallback or skip. An array default is avoided
    /// because the configuration binder appends bound elements to a pre-populated array.
    /// </remarks>
    public string[] ProviderKeys { get; init; } = [];

    /// <summary>Number of times each scenario is repeated per model and variant combination.</summary>
    /// <remarks>Defaults to <c>3</c>. A single sample says little about a probabilistic model.</remarks>
    [Range(1, 100)]
    public int Repetitions { get; init; } = 3;

    /// <summary>Instruction variant names to evaluate; empty evaluates every variant a scenario defines.</summary>
    public string[] InstructionVariants { get; init; } = [];

    /// <summary>Tool surface variant names to evaluate; empty evaluates every variant a scenario defines.</summary>
    public string[] ToolSurfaceVariants { get; init; } = [];

    /// <summary>
    /// Minimum pass rate the baseline instruction and tool surface must reach for each model.
    /// </summary>
    /// <remarks>Defaults to <c>0.5</c>. Other variants are reported for comparison but not asserted.</remarks>
    [Range(0d, 1d)]
    public double MinimumPassRate { get; init; } = 0.5;

    /// <summary>Maximum duration of a single agent run, including sub-agent delegation, in seconds.</summary>
    /// <remarks>Defaults to <c>300</c>, the <see cref="AgentExtensions.RunAnalysisAsync"/> default.</remarks>
    [Range(1, 3600)]
    public int RunTimeoutSeconds { get; init; } = 300;

    /// <summary>Maximum duration of the unmeasured warm-up request sent to each provider, in seconds.</summary>
    /// <remarks>Defaults to <c>1800</c>, because a model router may need minutes to swap a large model into memory.</remarks>
    [Range(1, 7200)]
    public int WarmUpTimeoutSeconds { get; init; } = 1800;

    /// <summary>
    /// Directory receiving <c>runs.jsonl</c> and <c>summary.md</c>, relative to the test output directory
    /// unless rooted.
    /// </summary>
    /// <remarks>Defaults to <c>TestResults/AgentEvaluation</c>, which the repository ignores.</remarks>
    [Required, MinLength(1)]
    public string ResultsDirectory { get; init; } = Path.Combine("TestResults", "AgentEvaluation");

    /// <summary>
    /// Whether each provider's <see cref="ProviderConfig.ReasoningEffort"/> is sent with every request of the
    /// evaluated agent tree.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>, matching <see cref="AgentExtensions.CreateAgentTool"/> and callers that
    /// build chat options without the provider, so the configured effort is never sent. Enable it to measure
    /// what honouring the setting would change, for example disabling thinking on an edge model.
    /// </remarks>
    public bool ProviderReasoningEffortApplied { get; init; }

    /// <summary>
    /// Whether <see cref="ToolSource.Endpoint"/> sources are offered, mapped onto the in-process tools of the
    /// same names.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>, matching hosts that build agents with
    /// <see cref="AgentExtensions.CreateToolsForAgent"/> and so never loads Endpoint sources. Enable it to
    /// measure the surface the configuration intends, for hosts that load Endpoint sources themselves.
    /// </remarks>
    public bool EndpointToolsMapped { get; init; }

    /// <summary>
    /// Provider every other model's speed is compared with in <c>session.summary.md</c>, or
    /// <see langword="null"/> for the first evaluated provider.
    /// </summary>
    /// <remarks>Typically the production model, so the comparison reads as the gain from switching.</remarks>
    public string? ReferenceProviderKey { get; init; }
}
