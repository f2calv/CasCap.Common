namespace CasCap.Common.Models;

/// <summary>
/// A question put to one agent, the synthetic state its tools return, and how the run is graded.
/// </summary>
/// <remarks>
/// Tool names, descriptions and schemas come from the application's real MCP tool types, so the model sees
/// exactly what production sends. Only the tool responses are synthetic, which makes the ground truth known.
/// </remarks>
public sealed record AgentEvaluationScenario
{
    /// <summary>Stable kebab-case identifier used as the theory data row and in reports.</summary>
    public required string Id { get; init; }

    /// <summary>Key into <c>CasCap:AIConfig:Agents</c> of the agent that receives the question.</summary>
    public required string AgentKey { get; init; }

    /// <summary>The user message, phrased the way a real user would send it.</summary>
    public required string Question { get; init; }

    /// <summary>The check applied to the final answer.</summary>
    public required AnswerExpectation Answer { get; init; }

    /// <summary>
    /// Tool groups the run must call, at any delegation depth. Each group is satisfied when any one of its
    /// tool names was called, so alternatives that yield the same fact can share a group.
    /// </summary>
    public IReadOnlyList<string[]> RequiredToolGroups { get; init; } = [];

    /// <summary>Tool names the run must not call, at any delegation depth.</summary>
    public IReadOnlyList<string> ForbiddenTools { get; init; } = [];

    /// <summary>Whether calling any <see cref="ToolDisposition.SideEffect"/> tool fails the run.</summary>
    /// <remarks>Defaults to <see langword="true"/>: a question must never switch, move, unlock or send anything.</remarks>
    public bool SideEffectsForbidden { get; init; } = true;

    /// <summary>
    /// Responses keyed by model-facing tool name. The delegate receives the model's arguments and returns an
    /// object of the tool's real return type, which is serialized exactly as the real tool result would be.
    /// </summary>
    public IReadOnlyDictionary<string, Func<AIFunctionArguments, object?>> ToolResponses { get; init; }
        = new Dictionary<string, Func<AIFunctionArguments, object?>>();

    /// <summary>Instruction variants compared for this scenario; the first is the asserted baseline.</summary>
    public IReadOnlyList<InstructionVariant> InstructionVariants { get; init; } = [InstructionVariant.Baseline];

    /// <summary>Tool surface variants compared for this scenario; the first is the asserted baseline.</summary>
    public IReadOnlyList<ToolSurfaceVariant> ToolSurfaceVariants { get; init; } = [ToolSurfaceVariant.Baseline];

    /// <summary>Why the scenario exists and what a failure would suggest.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <inheritdoc/>
    public override string ToString() => Id;
}
