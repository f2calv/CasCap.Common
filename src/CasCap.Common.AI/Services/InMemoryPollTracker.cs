using System.Collections.Concurrent;

namespace CasCap.Services;

/// <summary>
/// In-memory implementation of <see cref="IPollTracker"/> that tracks active polls
/// and their votes with automatic TTL-based expiry.
/// </summary>
/// <remarks>
/// Polls older than <see cref="AIConfig.PollTtlMs"/> are automatically pruned during
/// <see cref="GetActivePolls"/> and <see cref="GetPoll"/> calls to prevent
/// unbounded memory growth.
/// </remarks>
public sealed class InMemoryPollTracker(IOptions<AIConfig> aiConfig) : IPollTracker
{
    private readonly ConcurrentDictionary<string, ActivePoll> _polls = new();

    private readonly TimeSpan _pollTtl = TimeSpan.FromMilliseconds(aiConfig.Value.PollTtlMs);

    /// <inheritdoc/>
    public void TrackPoll(string pollId, string question, string[] answers, string groupId) =>
        _polls[pollId] = new ActivePoll
        {
            PollId = pollId,
            Question = question,
            Answers = answers,
            GroupId = groupId,
        };

    /// <inheritdoc/>
    public bool RecordVote(string pollId, string voter, int[] selectedIndices)
    {
        if (!_polls.TryGetValue(pollId, out var poll))
            return false;

        poll.Votes[voter] = selectedIndices;
        return true;
    }

    /// <inheritdoc/>
    public ActivePoll? GetPoll(string pollId)
    {
        PruneExpired();
        return _polls.TryGetValue(pollId, out var poll) ? poll : null;
    }

    /// <inheritdoc/>
    public bool RemovePoll(string pollId) =>
        _polls.TryRemove(pollId, out _);

    /// <inheritdoc/>
    public IReadOnlyList<ActivePoll> GetActivePolls()
    {
        PruneExpired();
        return [.. _polls.Values];
    }

    private void PruneExpired()
    {
        var cutoff = DateTime.UtcNow - _pollTtl;
        foreach (var key in _polls.Keys.ToArray())
            if (_polls.TryGetValue(key, out var poll) && poll.CreatedUtc < cutoff)
                _polls.TryRemove(key, out _);
    }
}
