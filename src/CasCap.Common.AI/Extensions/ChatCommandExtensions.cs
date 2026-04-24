namespace CasCap.Extensions;

/// <summary>
/// Shared helpers for parsing and executing <see cref="ChatCommand"/> slash-commands.
/// Used by <see cref="CasCap.App.Console.ConsoleApp"/> and
/// <see cref="CasCap.Services.CommunicationsBgService"/>.
/// </summary>
public static class ChatCommandParser
{
    /// <summary>
    /// Pre-computed map of lower-case command prefix to <see cref="ChatCommand"/> value,
    /// initialized at type load time from the <see cref="DescriptionAttribute"/> on each enum member.
    /// Ordered longest-prefix-first so that <c>/session save</c> is matched before <c>/session</c>.
    /// </summary>
    public static readonly IReadOnlyList<KeyValuePair<string, ChatCommand>> CommandPrefixMap =
        Enum.GetValues<ChatCommand>()
            .Select(cmd => (cmd, prefix: typeof(ChatCommand).GetField(cmd.ToString())
                ?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty))
            .Where(t => !string.IsNullOrEmpty(t.prefix))
            .OrderByDescending(t => t.prefix.Length)
            .Select(t => new KeyValuePair<string, ChatCommand>(t.prefix.ToLowerInvariant(), t.cmd))
            .ToList();

    /// <summary>
    /// Human-readable one-line descriptions for each <see cref="ChatCommand"/>, used by both
    /// <c>/help</c> in the console UI and in the Signal-messenger help response.
    /// </summary>
    public static readonly IReadOnlyDictionary<ChatCommand, string> CommandDescriptions =
        new Dictionary<ChatCommand, string>
        {
            [ChatCommand.Help] = "List all available commands.",
            [ChatCommand.SessionInfo] = "Show session size in bytes, message count, and StateBag keys.",
            [ChatCommand.SessionReset] = "Discard the current session; the next message starts a fresh conversation.",
            [ChatCommand.SessionBypass] = "Send a one-off prompt without loading or saving the session. Usage: /session bypass <prompt>",
            [ChatCommand.SessionCompact] = "Reduce the session to the newest N messages. Usage: /session compact <count>",
            [ChatCommand.SessionDisable] = "Disable session persistence; each message starts a fresh conversation.",
            [ChatCommand.SessionEnable] = "Re-enable session persistence.",
            [ChatCommand.SessionSave] = "Save a named snapshot of the active session. Usage: /session save <name>",
            [ChatCommand.SessionLoad] = "Load a previously saved snapshot into the active session. Usage: /session load <name>",
            [ChatCommand.SessionDelete] = "Delete a previously saved session snapshot. Usage: /session delete <name>",
            [ChatCommand.Model] = "Override the model for subsequent requests. Usage: /model <modelName> (no arg = show current override).",
            [ChatCommand.Instructions] = "Replace the system instructions for subsequent requests. Usage: /instructions <text> (no arg = show current override).",
        };

    /// <summary>
    /// Attempts to parse a slash-command from the raw prompt line.
    /// Returns <see langword="true"/> when the line begins with a recognised <see cref="ChatCommand"/> prefix.
    /// </summary>
    /// <param name="promptLine">The raw user-entered text.</param>
    /// <param name="command">The matched command when <see langword="true"/> is returned.</param>
    /// <param name="argument">Any text that follows the command prefix (trimmed), or <see cref="string.Empty"/>.</param>
    public static bool TryParseCommand(string promptLine, out ChatCommand command, out string argument)
    {
        command = default;
        argument = string.Empty;

        var trimmed = promptLine.TrimStart();
        if (!trimmed.StartsWith('/'))
            return false;

        foreach (var (prefix, cmd) in CommandPrefixMap)
        {
            if (trimmed.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                command = cmd;
                argument = trimmed.Length > prefix.Length
                    ? trimmed[(prefix.Length + 1)..].Trim()
                    : string.Empty;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Compacts the in-memory chat history of <paramref name="session"/> to the newest
    /// <paramref name="keepCount"/> messages by removing older messages from the front.
    /// </summary>
    /// <param name="session">The session whose chat history will be compacted.</param>
    /// <param name="keepCount">The maximum number of messages to retain.</param>
    /// <param name="removedCount">The number of messages removed. Zero when no action was taken.</param>
    /// <returns>
    /// <see langword="true"/> when chat history was found in the session (even if nothing was removed);
    /// <see langword="false"/> when no in-memory chat history is present.
    /// </returns>
    /// <remarks>
    /// Uses <see cref="AgentSessionExtensions.TryGetInMemoryChatHistory"/> and
    /// <see cref="AgentSessionExtensions.SetInMemoryChatHistory"/> to read and write the
    /// chat history stored in the session's <see cref="AgentSession.StateBag"/>.
    /// </remarks>
    public static bool TryCompactSession(AgentSession session, int keepCount, out int removedCount)
    {
        removedCount = 0;

        if (!session.TryGetInMemoryChatHistory(out var messages))
            return false;

        var excess = messages.Count - keepCount;
        if (excess <= 0)
            return true;

        messages.RemoveRange(0, excess);
        session.SetInMemoryChatHistory(messages);
        removedCount = excess;
        return true;
    }

    /// <summary>
    /// Iterates over the <see cref="AgentSessionStateBag"/> entries in <paramref name="session"/>
    /// and returns a summary of each entry's key, byte size, and message count (for chat-history entries).
    /// </summary>
    /// <param name="session">The session to inspect.</param>
    /// <returns>
    /// A list of <see cref="StateBagEntry"/> records; empty when the StateBag has no entries
    /// or if serialization fails.
    /// </returns>
    public static IReadOnlyList<StateBagEntry> GetStateBagEntries(AgentSession session)
    {
        var result = new List<StateBagEntry>();
        try
        {
            var bagJson = session.StateBag.Serialize();
            foreach (var prop in bagJson.EnumerateObject())
            {
                var entryBytes = Encoding.UTF8.GetByteCount(prop.Value.GetRawText());
                var keyLabel = string.IsNullOrEmpty(prop.Name) ? "(default)" : prop.Name;
                int msgCount = 0, userCount = 0, assistantCount = 0;
                if (prop.Value.ValueKind is System.Text.Json.JsonValueKind.Object
                    && (prop.Value.TryGetProperty("Messages", out var msgArray)
                        || prop.Value.TryGetProperty("messages", out msgArray))
                    && msgArray.ValueKind is System.Text.Json.JsonValueKind.Array)
                {
                    msgCount = msgArray.GetArrayLength();
                    foreach (var msg in msgArray.EnumerateArray())
                    {
                        if ((msg.TryGetProperty("Role", out var role) || msg.TryGetProperty("role", out role))
                            && role.ValueKind is System.Text.Json.JsonValueKind.String)
                        {
                            var r = role.GetString();
                            if (string.Equals(r, "user", StringComparison.OrdinalIgnoreCase))
                                userCount++;
                            else if (string.Equals(r, "assistant", StringComparison.OrdinalIgnoreCase))
                                assistantCount++;
                        }
                    }
                }
                result.Add(new StateBagEntry(keyLabel, entryBytes, msgCount, userCount, assistantCount));
            }
        }
        catch { /* StateBag breakdown is best-effort */ }
        return result;
    }
}
