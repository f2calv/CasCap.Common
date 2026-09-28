using System.Text.Json.Serialization;

namespace CasCap.Common.Extensions;

/// <summary>Aggregates evaluation runs and renders them as Markdown and JSON Lines.</summary>
public static class AgentEvaluationReport
{
    private static readonly JsonSerializerOptions JsonLinesOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private const string PlainChatFileName = "chat.jsonl";

    /// <summary>Folder name shared by every scenario written during this test session.</summary>
    public static string SessionFolderName { get; } = TimeProvider.System.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

    /// <summary>Groups runs by scenario, model and variants, preserving first-seen order.</summary>
    /// <param name="runs">The runs to aggregate.</param>
    public static IReadOnlyList<AgentEvaluationSummary> Summarise(IEnumerable<AgentEvaluationRun> runs) =>
        runs
            .GroupBy(r => (r.ScenarioId, r.ProviderKey, r.ModelName, r.InstructionVariant, r.ToolSurfaceVariant))
            .Select(g => Summarise(g.Key, [.. g]))
            .ToList();

    /// <summary>
    /// Returns the 95% Wilson score interval for a binomial proportion, which stays meaningful for the small
    /// sample sizes typical of model evaluation.
    /// </summary>
    /// <param name="successes">Number of successes.</param>
    /// <param name="trials">Number of trials.</param>
    /// <param name="z">Standard normal quantile; <c>1.96</c> gives a 95% interval.</param>
    public static (double Lower, double Upper) WilsonInterval(int successes, int trials, double z = 1.96)
    {
        if (trials == 0)
            return (0d, 1d);

        var p = (double)successes / trials;
        var z2 = z * z;
        var denominator = 1 + z2 / trials;
        var centre = (p + z2 / (2 * trials)) / denominator;
        var halfWidth = z * Math.Sqrt(p * (1 - p) / trials + z2 / (4d * trials * trials)) / denominator;
        return (Math.Max(0d, centre - halfWidth), Math.Min(1d, centre + halfWidth));
    }

    /// <summary>Renders summaries as a Markdown table, one row per model and variant combination.</summary>
    /// <param name="summaries">The summaries to render.</param>
    public static string ToMarkdown(IEnumerable<AgentEvaluationSummary> summaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Scenario | Provider | Model | Instructions | Tools | Pass | 95% CI | Answer | Tool choice | Errors | Median | Max | Requests | Tool calls | Input tok | Output tok | Throttled | Thinking runs | Top failures |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var s in summaries)
            sb.AppendLine(Invariant($"| {s.ScenarioId} | {s.ProviderKey} | {Escape(s.ModelName)} | {s.InstructionVariant} | {s.ToolSurfaceVariant} | {s.Passed}/{s.Runs} | {s.PassRateLower:P0}–{s.PassRateUpper:P0} | {s.AnswersCorrect}/{s.Runs} | {s.ToolSelectionsCorrect}/{s.Runs} | {s.Errors} | {Seconds(s.MedianElapsed)} | {Seconds(s.MaxElapsed)} | {s.MeanRoundTrips:0.#} | {s.MeanToolCalls:0.#} | {Number(s.MeanInputTokens)} | {Number(s.MeanOutputTokens)} | {s.ThrottledResponses} | {s.RunsWithThinking} | {Escape(string.Join("; ", s.TopFailureReasons))} |"));
        return sb.ToString();
    }

    /// <summary>
    /// Renders every model request of a run in order, showing where time and context go across delegation.
    /// </summary>
    /// <param name="run">The run to render.</param>
    public static string ToTimelineMarkdown(AgentEvaluationRun run)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"Run {run.ScenarioId} / {run.ProviderKey} / {run.InstructionVariant} / {run.ToolSurfaceVariant} #{run.Repetition}: {Seconds(run.Elapsed)}, tools called: {(run.ToolCalls.Count == 0 ? "none" : string.Join(", ", run.ToolCalls.Select(c => $"{c.AgentKey}.{c.ToolName}")))}"));
        sb.AppendLine();
        sb.AppendLine("| # | Agent | Elapsed | Messages | Instruction chars | Tools | Tool schema chars (~tok) | Input tok | Cached tok | Output tok | Reasoning tok | Thinking chars | Finish |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        var index = 0;
        foreach (var r in run.RoundTrips)
            sb.AppendLine(Invariant($"| {++index} | {r.AgentKey} | {Seconds(r.Elapsed)} | {r.MessageCount} | {r.InstructionCharacters} | {r.ToolCount} | {r.ToolSchemaCharacters} (~{ToolFootprint.ApproximateTokens(r.ToolSchemaCharacters)}) | {Number(r.InputTokens)} | {Number(r.CachedInputTokens)} | {Number(r.OutputTokens)} | {Number(r.ReasoningTokens)} | {r.ReasoningCharacters} | {r.FinishReason ?? "-"} |"));
        return sb.ToString();
    }

    /// <summary>
    /// Writes <c>{scenarioId}.runs.jsonl</c> and <c>{scenarioId}.summary.md</c> into a per-session folder.
    /// </summary>
    /// <param name="rootDirectory">The results root; relative paths resolve against the test output directory.</param>
    /// <param name="scenarioId">The scenario the runs belong to.</param>
    /// <param name="runs">The runs to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The folder written to.</returns>
    public static async Task<string> WriteAsync(string rootDirectory, string scenarioId, IReadOnlyList<AgentEvaluationRun> runs,
        CancellationToken cancellationToken)
    {
        var directory = GetSessionDirectory(rootDirectory);

        var lines = runs.Select(r => JsonSerializer.Serialize(r, JsonLinesOptions));
        await File.WriteAllLinesAsync(Path.Combine(directory, $"{scenarioId}.runs.jsonl"), lines, cancellationToken).ConfigureAwait(false);

        var markdown = new StringBuilder()
            .AppendLine($"# {scenarioId}")
            .AppendLine()
            .AppendLine(ToMarkdown(Summarise(runs)))
            .AppendLine("## Request timelines")
            .AppendLine();
        foreach (var run in runs)
            markdown.AppendLine(ToTimelineMarkdown(run));
        await File.WriteAllTextAsync(Path.Combine(directory, $"{scenarioId}.summary.md"), markdown.ToString(), cancellationToken).ConfigureAwait(false);

        return directory;
    }

    /// <summary>
    /// Summarises each model across every scenario and variant, with its speed relative to a reference model.
    /// </summary>
    /// <param name="runs">Runs from any number of scenarios.</param>
    /// <param name="referenceProviderKey">The provider other models are compared with, typically the edge model.</param>
    /// <param name="plainChat">Tool-free request durations keyed by provider, or <see langword="null"/>.</param>
    public static IReadOnlyList<AgentEvaluationProviderSummary> SummariseProviders(IReadOnlyList<AgentEvaluationRun> runs,
        string referenceProviderKey, IReadOnlyDictionary<string, List<TimeSpan>>? plainChat = null)
    {
        var cellMedians = runs
            .GroupBy(r => (r.ProviderKey, Cell: (r.ScenarioId, r.InstructionVariant, r.ToolSurfaceVariant)))
            .ToDictionary(g => g.Key, g => Percentile(g.Select(r => r.Elapsed).ToList(), 0.5));

        return runs
            .GroupBy(r => (r.ProviderKey, r.ModelName))
            .Select(g =>
            {
                var providerRuns = g.ToList();
                var inputTokens = providerRuns.Where(r => r.InputTokens is not null).Select(r => (double)r.InputTokens!.Value).ToList();
                var outputTokens = providerRuns.Where(r => r.OutputTokens is not null).Select(r => (double)r.OutputTokens!.Value).ToList();
                var ratios = cellMedians
                    .Where(c => c.Key.ProviderKey == g.Key.ProviderKey)
                    .Select(c => (Provider: c.Value, Reference: cellMedians.GetValueOrDefault((referenceProviderKey, c.Key.Cell))))
                    .Where(c => c.Provider > TimeSpan.Zero && c.Reference > TimeSpan.Zero)
                    .Select(c => c.Reference.TotalSeconds / c.Provider.TotalSeconds)
                    .ToList();

                return new AgentEvaluationProviderSummary
                {
                    ProviderKey = g.Key.ProviderKey,
                    ModelName = g.Key.ModelName,
                    Runs = providerRuns.Count,
                    Passed = providerRuns.Count(r => r.Passed),
                    ToolSelectionsCorrect = providerRuns.Count(r => r.ToolSelectionPassed),
                    Errors = providerRuns.Count(r => r.Error is not null),
                    MedianRun = Percentile(providerRuns.Select(r => r.Elapsed).ToList(), 0.5),
                    P90Run = Percentile(providerRuns.Select(r => r.Elapsed).ToList(), 0.9),
                    MedianRequest = Percentile(providerRuns.SelectMany(r => r.RoundTrips).Select(r => r.Elapsed).ToList(), 0.5),
                    MedianPlainChat = plainChat?.GetValueOrDefault(g.Key.ProviderKey) is { Count: > 0 } chats ? Percentile(chats, 0.5) : null,
                    MeanRequests = providerRuns.Average(r => r.RoundTrips.Count),
                    MeanInputTokens = inputTokens.Count == 0 ? null : inputTokens.Average(),
                    MeanOutputTokens = outputTokens.Count == 0 ? null : outputTokens.Average(),
                    ThrottledResponses = providerRuns.Sum(r => r.ThrottledResponses),
                    RunsWithThinking = providerRuns.Count(r => r.ReasoningCharacters > 0),
                    SpeedupVersusReference = ratios.Count == 0 ? null : Math.Exp(ratios.Average(Math.Log)),
                };
            })
            .OrderBy(s => s.MedianRun)
            .ToList();
    }

    /// <summary>Renders provider summaries as a Markdown table.</summary>
    /// <param name="summaries">The summaries to render.</param>
    /// <param name="referenceProviderKey">The provider the speed-up column is relative to.</param>
    public static string ToProviderMarkdown(IEnumerable<AgentEvaluationProviderSummary> summaries, string referenceProviderKey)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"| Provider | Model | Pass | Tool choice | Errors | Plain chat | Median run | P90 run | Median request | Requests/run | Input tok/run | Output tok/run | Throttled | Thinking runs | × faster than {referenceProviderKey} |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var s in summaries)
            sb.AppendLine(Invariant($"| {s.ProviderKey} | {Escape(s.ModelName)} | {s.Passed}/{s.Runs} ({s.PassRate:P0}) | {s.ToolSelectionsCorrect}/{s.Runs} | {s.Errors} | {(s.MedianPlainChat is { } chat ? Seconds(chat) : "-")} | {Seconds(s.MedianRun)} | {Seconds(s.P90Run)} | {Seconds(s.MedianRequest)} | {s.MeanRequests:0.#} | {Number(s.MeanInputTokens)} | {Number(s.MeanOutputTokens)} | {s.ThrottledResponses} | {s.RunsWithThinking} | {(s.SpeedupVersusReference is { } x ? $"{x:0.0}×" : "-")} |"));
        return sb.ToString();
    }

    /// <summary>
    /// Rewrites <c>session.summary.md</c> from every <c>*.runs.jsonl</c> in the session folder, so the
    /// cross-model comparison covers all scenarios written so far.
    /// </summary>
    /// <param name="directory">The session folder returned by <see cref="WriteAsync"/>.</param>
    /// <param name="referenceProviderKey">The provider other models are compared with.</param>
    /// <param name="cancellationToken">Cancels the read and write.</param>
    /// <returns>The rendered comparison table.</returns>
    public static async Task<string> WriteSessionSummaryAsync(string directory, string referenceProviderKey,
        CancellationToken cancellationToken)
    {
        var runs = new List<AgentEvaluationRun>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.runs.jsonl").Order(StringComparer.Ordinal))
            foreach (var line in await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false))
                if (!string.IsNullOrWhiteSpace(line))
                    runs.Add(JsonSerializer.Deserialize<AgentEvaluationRun>(line, JsonLinesOptions)!);

        var plainChat = new Dictionary<string, List<TimeSpan>>();
        var chatFile = Path.Combine(directory, PlainChatFileName);
        if (File.Exists(chatFile))
            foreach (var line in await File.ReadAllLinesAsync(chatFile, cancellationToken).ConfigureAwait(false))
                if (JsonSerializer.Deserialize<PlainChatSample>(line, JsonLinesOptions) is { } sample)
                    (plainChat.TryGetValue(sample.ProviderKey, out var list) ? list : plainChat[sample.ProviderKey] = []).Add(sample.Elapsed);

        var table = ToProviderMarkdown(SummariseProviders(runs, referenceProviderKey, plainChat), referenceProviderKey);
        var scenarios = string.Join(", ", runs.Select(r => r.ScenarioId).Distinct());
        await File.WriteAllTextAsync(Path.Combine(directory, "session.summary.md"),
            $"# Agent evaluation session {SessionFolderName}\n\nScenarios: {scenarios}\n\n{table}", cancellationToken).ConfigureAwait(false);
        return table;
    }

    /// <summary>
    /// Appends a tool-free request duration to <c>chat.jsonl</c>, giving each model's plain chat latency
    /// alongside its agent run latency.
    /// </summary>
    /// <param name="rootDirectory">The results root; relative paths resolve against the test output directory.</param>
    /// <param name="providerKey">The provider measured.</param>
    /// <param name="elapsed">The request duration.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public static Task AppendPlainChatAsync(string rootDirectory, string providerKey, TimeSpan elapsed,
        CancellationToken cancellationToken) =>
        File.AppendAllLinesAsync(Path.Combine(GetSessionDirectory(rootDirectory), PlainChatFileName),
            [JsonSerializer.Serialize(new PlainChatSample(providerKey, elapsed), JsonLinesOptions)], cancellationToken);

    private static string GetSessionDirectory(string rootDirectory)
    {
        var directory = Path.Combine(Path.GetFullPath(rootDirectory, AppContext.BaseDirectory), SessionFolderName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static TimeSpan Percentile(IReadOnlyList<TimeSpan> values, double percentile)
    {
        if (values.Count == 0)
            return TimeSpan.Zero;

        var ordered = values.Order().ToList();
        return ordered[(int)Math.Ceiling(percentile * ordered.Count) - 1];
    }

    private static AgentEvaluationSummary Summarise(
        (string ScenarioId, string ProviderKey, string ModelName, string InstructionVariant, string ToolSurfaceVariant) key,
        IReadOnlyList<AgentEvaluationRun> runs)
    {
        var passed = runs.Count(r => r.Passed);
        var (lower, upper) = WilsonInterval(passed, runs.Count);
        var elapsed = runs.Select(r => r.Elapsed).Order().ToList();
        var inputTokens = runs.Where(r => r.InputTokens is not null).Select(r => (double)r.InputTokens!.Value).ToList();
        var outputTokens = runs.Where(r => r.OutputTokens is not null).Select(r => (double)r.OutputTokens!.Value).ToList();

        return new AgentEvaluationSummary
        {
            ScenarioId = key.ScenarioId,
            ProviderKey = key.ProviderKey,
            ModelName = key.ModelName,
            InstructionVariant = key.InstructionVariant,
            ToolSurfaceVariant = key.ToolSurfaceVariant,
            Runs = runs.Count,
            Passed = passed,
            AnswersCorrect = runs.Count(r => r.AnswerPassed),
            ToolSelectionsCorrect = runs.Count(r => r.ToolSelectionPassed),
            Errors = runs.Count(r => r.Error is not null),
            ThrottledResponses = runs.Sum(r => r.ThrottledResponses),
            RunsWithThinking = runs.Count(r => r.ReasoningCharacters > 0),
            PassRateLower = lower,
            PassRateUpper = upper,
            MedianElapsed = elapsed.Count == 0 ? TimeSpan.Zero : elapsed[(elapsed.Count - 1) / 2],
            MaxElapsed = elapsed.Count == 0 ? TimeSpan.Zero : elapsed[^1],
            MeanRoundTrips = runs.Count == 0 ? 0d : runs.Average(r => r.RoundTrips.Count),
            MeanToolCalls = runs.Count == 0 ? 0d : runs.Average(r => r.ToolCalls.Count),
            MeanInputTokens = inputTokens.Count == 0 ? null : inputTokens.Average(),
            MeanOutputTokens = outputTokens.Count == 0 ? null : outputTokens.Average(),
            TopFailureReasons = runs
                .SelectMany(r => r.FailureReasons)
                .GroupBy(reason => reason)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key} ×{g.Count()}")
                .ToList(),
        };
    }

    private static string Seconds(TimeSpan value) => Invariant($"{value.TotalSeconds:0.0}s");

    private static string Number(double? value) => value is null ? "-" : Invariant($"{value:0}");

    private static string Number(long? value) => value is null ? "-" : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed record PlainChatSample(string ProviderKey, TimeSpan Elapsed);
}
