using System.Text.Json;

namespace CasCap.Services;

/// <summary>Shared handler for <see cref="ChatCommand"/> slash-commands and agent session management.</summary>
/// <remarks>
/// Encapsulates the command parsing, session load/save/reset/compact, enable/disable,
/// and named-snapshot logic common to both <see cref="CasCap.App.Console.ConsoleApp"/>
/// and <see cref="CommunicationsBgService"/>. Each consumer provides its own
/// <see cref="ISessionStore"/> for persistence.
/// </remarks>
public class AgentCommandHandler(ILogger<AgentCommandHandler> logger, IOptions<AIConfig> aiConfig, ISessionStore sessionStore)
{
    private readonly TimeSpan _sessionTtl = TimeSpan.FromDays(aiConfig.Value.SessionTtlDays);

    private string? _modelOverride;
    private string? _instructionsOverride;
    private bool _sessionEnabled = true;

    /// <summary>Current model override, or <see langword="null"/> when using the provider default.</summary>
    public string? ModelOverride => _modelOverride;

    /// <summary>Current instructions override, or <see langword="null"/> when using the configured default.</summary>
    public string? InstructionsOverride => _instructionsOverride;

    /// <summary>Whether session persistence is enabled. When <see langword="false"/>, each message starts a fresh conversation.</summary>
    public bool SessionEnabled => _sessionEnabled;

    /// <summary>
    /// Processes a recognised <see cref="ChatCommand"/> and returns a text response,
    /// or <see langword="null"/> when no reply is needed (e.g. <see cref="ChatCommand.SessionBypass"/>).
    /// </summary>
    /// <param name="command">The parsed command.</param>
    /// <param name="argument">Any text following the command prefix.</param>
    /// <param name="agent">The <see cref="AIAgent"/> used for session deserialisation.</param>
    /// <param name="agentName">The agent display name (<see cref="AgentConfig.Name"/>) used to derive the Redis session key.</param>
    /// <param name="onModelChanged">Optional callback invoked when the model override changes.</param>
    /// <returns>
    /// A response string to display/send, or <see langword="null"/> when the command produces no
    /// immediate reply (the caller may need to enqueue further work, e.g. for bypass).
    /// </returns>
    public async Task<string?> HandleCommandAsync(
        ChatCommand command,
        string argument,
        AIAgent agent,
        string agentName,
        Func<string, Task>? onModelChanged = null)
    {
        var sessionKey = BuildSessionKey(agentName);
        switch (command)
        {
            case ChatCommand.Help:
                return BuildHelpText();

            case ChatCommand.SessionInfo:
                return await BuildSessionInfoTextAsync(agent, sessionKey);

            case ChatCommand.SessionReset:
                await sessionStore.DeleteAsync(sessionKey);
                logger.LogInformation("{ClassName} session reset via slash command", nameof(AgentCommandHandler));
                return "Session reset. The next message will start a fresh conversation.";

            case ChatCommand.SessionBypass:
                if (string.IsNullOrWhiteSpace(argument))
                    return "Usage: /session bypass <prompt>";
                // Caller is responsible for enqueuing the bypass prompt.
                return null;

            case ChatCommand.SessionCompact:
                return await CompactSessionAsync(agent, sessionKey, argument);

            case ChatCommand.SessionDisable:
                _sessionEnabled = false;
                logger.LogInformation("{ClassName} session persistence disabled via slash command", nameof(AgentCommandHandler));
                return "Session persistence disabled. Each message will start a fresh conversation.";

            case ChatCommand.SessionEnable:
                _sessionEnabled = true;
                logger.LogInformation("{ClassName} session persistence enabled via slash command", nameof(AgentCommandHandler));
                return "Session persistence enabled.";

            case ChatCommand.SessionSave:
                return await SaveSnapshotAsync(agent, agentName, sessionKey, argument);

            case ChatCommand.SessionLoad:
                return await LoadSnapshotAsync(agent, agentName, sessionKey, argument);

            case ChatCommand.SessionDelete:
                return await DeleteSnapshotAsync(agentName, argument);

            case ChatCommand.Model:
                if (string.IsNullOrWhiteSpace(argument))
                    return $"Current model override: {_modelOverride ?? "(none — using provider default)"}";
                _modelOverride = argument;
                logger.LogInformation("{ClassName} model overridden to {Model} via slash command",
                    nameof(AgentCommandHandler), _modelOverride);
                if (onModelChanged is not null)
                    await onModelChanged(_modelOverride);
                return $"Model overridden to: {_modelOverride}";

            case ChatCommand.Instructions:
                if (string.IsNullOrWhiteSpace(argument))
                {
                    if (_instructionsOverride is null)
                        return "Current instructions override: (none — using configured default)";
                    var preview = _instructionsOverride.Length > 200
                        ? _instructionsOverride[..200] + "..."
                        : _instructionsOverride;
                    return $"Current instructions override ({_instructionsOverride.Length} chars): {preview}";
                }
                _instructionsOverride = argument;
                logger.LogInformation("{ClassName} instructions overridden via slash command ({Length} chars)",
                    nameof(AgentCommandHandler), _instructionsOverride.Length);
                return $"Instructions overridden ({_instructionsOverride.Length} chars).";

            default:
                return null;
        }
    }

    /// <summary>Loads the agent session from the store, or returns <see langword="null"/> for a new conversation.</summary>
    /// <remarks>Returns <see langword="null"/> immediately when <see cref="SessionEnabled"/> is <see langword="false"/>.</remarks>
    public async Task<AgentSession?> LoadSessionAsync(AIAgent agent, string agentName)
    {
        if (!_sessionEnabled)
            return null;
        var sessionKey = BuildSessionKey(agentName);
        var json = await sessionStore.GetAsync(sessionKey);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        JsonElement reloaded = json.FromJson<JsonElement>(JsonSerializerOptions.Web)!;
        return await agent.DeserializeSessionAsync(reloaded, JsonSerializerOptions.Web);
    }

    /// <summary>Persists the agent session to the store with a 7-day sliding expiration.</summary>
    /// <remarks>No-op when <see cref="SessionEnabled"/> is <see langword="false"/>.</remarks>
    public async Task SaveSessionAsync(AIAgent agent, string agentName, AgentSession session)
    {
        if (!_sessionEnabled)
            return;
        var sessionKey = BuildSessionKey(agentName);
        var serialized = await agent.SerializeSessionAsync(session, JsonSerializerOptions.Web);
        await sessionStore.SetAsync(sessionKey, serialized.ToJson(), _sessionTtl);
    }

    /// <summary>Applies <see cref="ModelOverride"/> to <paramref name="chatOptions"/> when set.</summary>
    public void ApplyModelOverride(ChatOptions chatOptions)
    {
        if (!string.IsNullOrWhiteSpace(_modelOverride))
            chatOptions.ModelId = _modelOverride;
    }

    /// <summary>Applies <see cref="InstructionsOverride"/> to <paramref name="chatOptions"/> when set, wrapping with shared prefix/suffix from <paramref name="aiConfig"/>.</summary>
    public void ApplyInstructionsOverride(ChatOptions chatOptions, AIConfig? aiConfig = null)
    {
        if (!string.IsNullOrWhiteSpace(_instructionsOverride))
            chatOptions.Instructions = AgentExtensions.WrapInstructions(_instructionsOverride, aiConfig);
    }

    #region private helpers

    /// <summary>Builds the <c>/help</c> response text listing all commands and descriptions.</summary>
    private static string BuildHelpText()
    {
        var sb = new StringBuilder();
        foreach (var (prefix, cmd) in ChatCommandParser.CommandPrefixMap)
        {
            var description = ChatCommandParser.CommandDescriptions.GetValueOrDefault(cmd, string.Empty);
            sb.AppendLine($"{prefix} — {description}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Builds the <c>/session info</c> response text with size and StateBag breakdown.</summary>
    private async Task<string> BuildSessionInfoTextAsync(AIAgent agent, string sessionKey)
    {
        var json = await sessionStore.GetAsync(sessionKey);
        if (string.IsNullOrWhiteSpace(json))
            return "No active session.";
        var sizeBytes = Encoding.UTF8.GetByteCount(json);
        try
        {
            var sessionElement = json.FromJson<JsonElement>(JsonSerializerOptions.Web)!;
            var session = await agent.DeserializeSessionAsync(sessionElement, JsonSerializerOptions.Web);
            var entries = ChatCommandParser.GetStateBagEntries(session);
            var lines = new StringBuilder();
            lines.AppendLine($"Session active (persistence: {(_sessionEnabled ? "on" : "off")}). Size: {sizeBytes:N0} bytes.");
            foreach (var e in entries)
            {
                var detail = e.MessageCount > 0
                    ? $", {e.MessageCount} messages ({e.UserMessageCount}u/{e.AssistantMessageCount}a)"
                    : string.Empty;
                lines.AppendLine($"  [{e.Key}] {e.ByteSize:N0} bytes{detail}");
            }
            return lines.ToString().TrimEnd();
        }
        catch
        {
            return $"Session active. Size: {sizeBytes:N0} bytes. (StateBag detail unavailable)";
        }
    }

    /// <summary>Handles the <c>/session compact</c> command.</summary>
    private async Task<string> CompactSessionAsync(AIAgent agent, string sessionKey, string argument)
    {
        if (!int.TryParse(argument, out var keepCount) || keepCount <= 0)
            return "Usage: /session compact <count> (positive integer)";
        var json = await sessionStore.GetAsync(sessionKey);
        if (string.IsNullOrWhiteSpace(json))
            return "No active session to compact.";
        try
        {
            var sessionElement = json.FromJson<JsonElement>(JsonSerializerOptions.Web)!;
            var session = await agent.DeserializeSessionAsync(sessionElement, JsonSerializerOptions.Web);
            if (!ChatCommandParser.TryCompactSession(session, keepCount, out var removedCount))
                return "No in-memory chat history found in the current session.";
            var serialized = await agent.SerializeSessionAsync(session, JsonSerializerOptions.Web);
            await sessionStore.SetAsync(sessionKey, serialized.ToJson(), _sessionTtl);
            return removedCount > 0
                ? $"Session compacted: removed {removedCount} message(s), {keepCount} retained."
                : $"Session already has {keepCount} or fewer messages — nothing to compact.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ClassName} session compact failed", nameof(AgentCommandHandler));
            return $"Session compact failed: {ex.Message}";
        }
    }

    /// <summary>Copies the active session to a named snapshot key.</summary>
    private async Task<string> SaveSnapshotAsync(AIAgent agent, string agentName, string sessionKey, string snapshotName)
    {
        if (string.IsNullOrWhiteSpace(snapshotName))
            return "Usage: /session save <name>";
        var json = await sessionStore.GetAsync(sessionKey);
        if (string.IsNullOrWhiteSpace(json))
            return "No active session to save.";
        var snapshotKey = BuildSnapshotKey(agentName, snapshotName);
        await sessionStore.SetAsync(snapshotKey, json, _sessionTtl);
        var sizeBytes = Encoding.UTF8.GetByteCount(json);
        logger.LogInformation("{ClassName} session snapshot saved as {SnapshotName} ({SizeBytes} bytes)",
            nameof(AgentCommandHandler), snapshotName, sizeBytes);
        return $"Session saved as \"{snapshotName}\" ({sizeBytes:N0} bytes).";
    }

    /// <summary>Loads a named snapshot into the active session key.</summary>
    private async Task<string> LoadSnapshotAsync(AIAgent agent, string agentName, string sessionKey, string snapshotName)
    {
        if (string.IsNullOrWhiteSpace(snapshotName))
            return "Usage: /session load <name>";
        var snapshotKey = BuildSnapshotKey(agentName, snapshotName);
        var json = await sessionStore.GetAsync(snapshotKey);
        if (string.IsNullOrWhiteSpace(json))
            return $"No snapshot named \"{snapshotName}\" found.";
        await sessionStore.SetAsync(sessionKey, json, _sessionTtl);
        var sizeBytes = Encoding.UTF8.GetByteCount(json);
        logger.LogInformation("{ClassName} session snapshot {SnapshotName} loaded into active session ({SizeBytes} bytes)",
            nameof(AgentCommandHandler), snapshotName, sizeBytes);
        return $"Snapshot \"{snapshotName}\" loaded into active session ({sizeBytes:N0} bytes).";
    }

    /// <summary>Deletes a named snapshot.</summary>
    private async Task<string> DeleteSnapshotAsync(string agentName, string snapshotName)
    {
        if (string.IsNullOrWhiteSpace(snapshotName))
            return "Usage: /session delete <name>";
        var snapshotKey = BuildSnapshotKey(agentName, snapshotName);
        await sessionStore.DeleteAsync(snapshotKey);
        logger.LogInformation("{ClassName} session snapshot {SnapshotName} deleted",
            nameof(AgentCommandHandler), snapshotName);
        return $"Snapshot \"{snapshotName}\" deleted.";
    }

    private static string BuildSessionKey(string agentName) =>
        $"agents:sessions:{agentName.ToLowerInvariant()}:active";

    private static string BuildSnapshotKey(string agentName, string snapshotName) =>
        $"agents:sessions:{agentName.ToLowerInvariant()}:{snapshotName.ToLowerInvariant()}";

    #endregion
}
