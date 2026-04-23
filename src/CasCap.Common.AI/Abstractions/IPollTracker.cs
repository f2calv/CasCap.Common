namespace CasCap.Abstractions;

/// <summary>
/// Tracks active polls created by the agent and records incoming votes so
/// that poll results can be fed back to the agent for decision-making.
/// </summary>
/// <remarks>
/// Provider-agnostic — designed to work with any messaging back-end
/// (Signal, Telegram, etc.) that supports polls.
/// </remarks>
public interface IPollTracker
{
    /// <summary>Registers a newly created poll for tracking.</summary>
    /// <param name="pollId">The unique identifier returned by the messaging API when the poll was created.</param>
    /// <param name="question">The poll question text.</param>
    /// <param name="answers">The answer options in display order.</param>
    /// <param name="groupId">The group or chat the poll was sent to.</param>
    void TrackPoll(string pollId, string question, string[] answers, string groupId);

    /// <summary>Records a vote cast by a group member against a tracked poll.</summary>
    /// <param name="pollId">The unique identifier of the poll.</param>
    /// <param name="voter">The voter identifier (provider-specific, e.g. phone number, user ID).</param>
    /// <param name="selectedIndices">Zero-based indices of the selected answers.</param>
    /// <returns><see langword="true"/> if the poll was found and the vote recorded; otherwise <see langword="false"/>.</returns>
    bool RecordVote(string pollId, string voter, int[] selectedIndices);

    /// <summary>Retrieves the current state of a tracked poll.</summary>
    /// <param name="pollId">The unique identifier of the poll.</param>
    /// <returns>The poll state, or <see langword="null"/> if no poll with that identifier is tracked.</returns>
    ActivePoll? GetPoll(string pollId);

    /// <summary>Removes a poll from tracking (e.g. after it has been closed or acted upon).</summary>
    /// <param name="pollId">The unique identifier of the poll.</param>
    /// <returns><see langword="true"/> if the poll was found and removed; otherwise <see langword="false"/>.</returns>
    bool RemovePoll(string pollId);

    /// <summary>Returns all currently tracked polls.</summary>
    IReadOnlyList<ActivePoll> GetActivePolls();
}
