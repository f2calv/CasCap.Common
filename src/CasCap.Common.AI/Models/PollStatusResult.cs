namespace CasCap.Models;

/// <summary>
/// Result object returned by the <c>get_poll_status</c> MCP tool, summarising the
/// current vote tally for a tracked poll.
/// </summary>
public record PollStatusResult
{
    /// <summary>The unique identifier of the poll.</summary>
    [Description("Unique identifier of the poll.")]
    public required string PollId { get; init; }

    /// <summary>The poll question text.</summary>
    [Description("The poll question text.")]
    public required string Question { get; init; }

    /// <summary>The answer options in display order.</summary>
    [Description("Answer options in display order.")]
    public required string[] Answers { get; init; }

    /// <summary>Total number of voters who have responded.</summary>
    [Description("Total number of voters who have responded.")]
    public int TotalVotes { get; init; }

    /// <summary>Human-readable summary of the vote tally.</summary>
    [Description("Human-readable summary of the vote tally per option.")]
    public required string Summary { get; init; }

    /// <summary>Whether the agent has already acted on this poll's results.</summary>
    [Description("True if the agent has already acted on this poll's results.")]
    public bool IsActedUpon { get; init; }
}
