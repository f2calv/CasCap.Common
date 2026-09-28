namespace CasCap.Common.Extensions;

/// <summary>Grades an agent run against its scenario.</summary>
public static class AgentEvaluationGrader
{
    /// <summary>Applies the answer, required tool, forbidden tool and side-effect checks.</summary>
    /// <param name="scenario">The scenario defining the expectations.</param>
    /// <param name="answer">The final answer text.</param>
    /// <param name="toolCalls">Tool calls at every delegation depth.</param>
    /// <returns>The individual verdicts and the reasons for any failure.</returns>
    public static (bool AnswerPassed, bool ToolSelectionPassed, IReadOnlyList<string> FailureReasons) Grade(
        AgentEvaluationScenario scenario, string answer, IReadOnlyList<RecordedToolCall> toolCalls)
    {
        var answerPassed = !string.IsNullOrWhiteSpace(answer) && scenario.Answer.IsSatisfiedBy(answer);

        var called = toolCalls.Select(c => c.ToolName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toolReasons = new List<string>();
        foreach (var group in scenario.RequiredToolGroups.Where(g => !g.Any(called.Contains)))
            toolReasons.Add($"did not call {string.Join(" or ", group)}");

        foreach (var tool in scenario.ForbiddenTools.Where(called.Contains))
            toolReasons.Add($"called forbidden {tool}");

        if (scenario.SideEffectsForbidden)
            foreach (var tool in toolCalls.Where(c => c.Disposition is ToolDisposition.SideEffect).Select(c => c.ToolName).Distinct())
                toolReasons.Add($"attempted side effect {tool}");

        IReadOnlyList<string> reasons = answerPassed
            ? toolReasons
            : [$"answer does not satisfy: {scenario.Answer.Description}", .. toolReasons];
        return (answerPassed, toolReasons.Count == 0, reasons);
    }
}
