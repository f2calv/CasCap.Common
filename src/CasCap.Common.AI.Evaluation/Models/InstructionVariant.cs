namespace CasCap.Common.Models;

/// <summary>
/// A named rewrite of an agent's system instructions, used to compare instruction wording across models.
/// </summary>
/// <remarks>
/// The rewrite receives the agent key and its resolved baseline instructions, and applies to every agent
/// in the delegation tree, so a sub-agent sees the same variant as its parent.
/// </remarks>
public sealed record InstructionVariant
{
    /// <summary>The variant evaluated as the reference point for every scenario.</summary>
    public static InstructionVariant Baseline { get; } = new() { Name = "baseline", Notes = "Instructions as shipped." };

    /// <summary>Short, unique name used in reports and in <see cref="AgentEvaluationConfig.InstructionVariants"/>.</summary>
    public required string Name { get; init; }

    /// <summary>What the variant changes and why.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Returns the instructions for an agent key given its baseline instructions.</summary>
    public Func<string, string, string> Rewrite { get; init; } = (_, baseline) => baseline;

    /// <inheritdoc/>
    public override string ToString() => Name;
}
