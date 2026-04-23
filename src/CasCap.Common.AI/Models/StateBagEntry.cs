namespace CasCap.Extensions;

/// <summary>Summary of a single entry in the <see cref="Microsoft.Agents.AI.AgentSessionStateBag"/>.</summary>
/// <param name="Key">The StateBag key name.</param>
/// <param name="ByteSize">Serialized byte size of the entry.</param>
/// <param name="MessageCount">Total message count (zero for non-chat entries).</param>
/// <param name="UserMessageCount">Number of user-role messages.</param>
/// <param name="AssistantMessageCount">Number of assistant-role messages.</param>
public sealed record StateBagEntry(
    string Key,
    int ByteSize,
    int MessageCount,
    int UserMessageCount,
    int AssistantMessageCount);
