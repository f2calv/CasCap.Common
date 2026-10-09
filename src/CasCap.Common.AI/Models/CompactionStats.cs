namespace CasCap.Common.Models;

/// <summary>Summary of a single chat-history compaction pass.</summary>
/// <param name="InputCount">Total messages before compaction.</param>
/// <param name="OutputCount">Total messages after compaction.</param>
/// <param name="ToolDropped">Messages dropped because they consisted solely of tool content.</param>
/// <param name="WindowTrimmed">Messages dropped by the sliding window to meet <paramref name="Target"/>.</param>
/// <param name="Target">The configured maximum non-system message count.</param>
public readonly record struct CompactionStats(
    int InputCount,
    int OutputCount,
    int ToolDropped,
    int WindowTrimmed,
    int Target);