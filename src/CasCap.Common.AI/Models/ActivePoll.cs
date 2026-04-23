using System.Collections.Concurrent;

namespace CasCap.Models;

/// <summary>
/// Represents an active poll being tracked for vote results.
/// </summary>
/// <remarks>
/// Provider-agnostic — designed to work with any messaging back-end
/// that supports polls.
/// </remarks>
public sealed class ActivePoll
{
    /// <summary>The unique identifier returned by the messaging API when the poll was created.</summary>
    public required string PollId { get; init; }

    /// <summary>The poll question text.</summary>
    public required string Question { get; init; }

    /// <summary>The answer options in display order.</summary>
    public required string[] Answers { get; init; }

    /// <summary>The group or chat the poll was sent to.</summary>
    public required string GroupId { get; init; }

    /// <summary>When the poll was created (UTC).</summary>
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Votes received so far, keyed by voter identifier (provider-specific).
    /// Each value is the array of zero-based answer indices the voter selected.
    /// </summary>
    public ConcurrentDictionary<string, int[]> Votes { get; } = new();

    /// <summary>Whether the poll has been acted upon by the agent.</summary>
    public bool IsActedUpon { get; set; }

    /// <summary>
    /// Builds a human-readable summary of the current vote tally suitable for
    /// inclusion in an agent prompt.
    /// </summary>
    public string BuildResultSummary()
    {
        // Snapshot votes to avoid inconsistency if another thread records a vote
        // between reading the count and iterating the values.
        var snapshot = Votes.ToArray();

        var sb = new StringBuilder();
        sb.AppendLine($"Poll: \"{Question}\"");
        sb.AppendLine($"Options: {string.Join(", ", Answers.Select((a, i) => $"[{i}] {a}"))}");
        sb.AppendLine($"Total votes: {snapshot.Length}");

        if (snapshot.Length == 0)
        {
            sb.AppendLine("No votes received yet.");
            return sb.ToString();
        }

        // Tally per option.
        var tally = new int[Answers.Length];
        foreach (var (_, indices) in snapshot)
            foreach (var idx in indices)
                if (idx >= 0 && idx < tally.Length)
                    tally[idx]++;

        for (var i = 0; i < Answers.Length; i++)
            sb.AppendLine($"  [{i}] {Answers[i]}: {tally[i]} vote(s)");

        return sb.ToString();
    }
}
