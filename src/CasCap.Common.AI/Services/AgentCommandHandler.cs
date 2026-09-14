using System.Collections.Concurrent;
using System.Text.Json;

namespace CasCap.Common.Services;

/// <summary>Shared handler for <see cref="ChatCommand"/> slash-commands and agent session management.</summary>
/// <remarks>
/// <para>
/// Encapsulates the command parsing, session load/save/reset/compact, enable/disable,
/// and named-snapshot logic common to both <c>ConsoleApp</c> and
/// <c>CommunicationsBgService</c>. Each consumer provides its own
/// <see cref="ISessionStore"/> for persistence.
/// </para>
/// <para>
/// Slash-command overrides (<c>/model</c>, <c>/instructions</c>, <c>/session enable|disable</c>)
/// are held <b>per agent</b>. This type is typically registered as a singleton and is shared by
/// every agent and every conversation in the process, so a single set of fields would let one
/// conversation silently re-point another conversation's model.
/// </para>
/// <para>
/// TODO: overrides are still scoped per agent rather than per conversation, so two conversations
/// talking to the <i>same</i> agent continue to share them. Resolving that needs a caller-supplied
/// isolation key — mirror <c>Microsoft.Agents.AI.Hosting.AgentIsolationKeyProvider</c> /
/// <c>IsolationKeyScopedAgentSessionStore</c>, or move the overrides into
/// <c>AgentSession.StateBag</c> so they travel with the session. Deferred because
/// <c>Microsoft.Agents.AI.Hosting</c> is still preview (1.21.0-preview as of 2026-09-11).
/// See https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Hosting/AgentSessionStore.cs
/// </para>
/// </remarks>
public sealed class AgentCommandHandler(ILogger<AgentCommandHandler> logger, IOptions<AIConfig> aiConfig, ISessionStore sessionStore)
{
    private readonly TimeSpan _sessionTtl = TimeSpan.FromDays(aiConfig.Value.SessionTtlDays);

    /// <summary>Per-agent slash-command override state, keyed by <see cref="AgentConfig.Name"/>.</summary>
    private readonly ConcurrentDictionary<string, AgentOverrides> _overrides = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Immutable per-agent override state, swapped atomically via <see cref="UpdateOverrides"/>.</summary>
    private sealed record AgentOverrides(string? Model, string? Instructions, bool SessionEnabled)
    {
        public static readonly AgentOverrides Default = new(null, null, true);
    }

    private AgentOverrides GetOverrides(string agentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        return _overrides.TryGetValue(agentName, out var overrides) ? overrides : AgentOverrides.Default;
    }

    private AgentOverrides UpdateOverrides(string agentName, Func<AgentOverrides, AgentOverrides> update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        return _overrides.AddOrUpdate(agentName,
            _ => update(AgentOverrides.Default),
            (_, existing) => update(existing));
    }

    /// <summary>Gets the model override for <paramref name="agentName"/>, or <see langword="null"/> when using the provider default.</summary>
    /// <param name="agentName">The agent display name (<see cref="AgentConfig.Name"/>).</param>
    public string? GetModelOverride(string agentName) => GetOverrides(agentName).Model;

    /// <summary>Gets the instructions override for <paramref name="agentName"/>, or <see langword="null"/> when using the configured default.</summary>
    /// <param name="agentName">The agent display name (<see cref="AgentConfig.Name"/>).</param>
    public string? GetInstructionsOverride(string agentName) => GetOverrides(agentName).Instructions;

    /// <summary>Gets whether session persistence is enabled for <paramref name="agentName"/>. When <see langword="false"/>, each message starts a fresh conversation.</summary>
    /// <param name="agentName">The agent display name (<see cref="AgentConfig.Name"/>).</param>
    public bool IsSessionEnabled(string agentName) => GetOverrides(agentName).SessionEnabled;

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
                return await BuildSessionInfoTextAsync(agent, agentName, sessionKey).ConfigureAwait(false);

            case ChatCommand.SessionReset:
                await sessionStore.DeleteAsync(sessionKey).ConfigureAwait(false);
                logger.LogInformation("{ClassName} session reset via slash command", nameof(AgentCommandHandler));
                return "Session reset. The next message will start a fresh conversation.";

            case ChatCommand.SessionBypass:
                if (string.IsNullOrWhiteSpace(argument))
                    return "Usage: /session bypass <prompt>";
                // Caller is responsible for enqueuing the bypass prompt.
                return null;

            case ChatCommand.SessionCompact:
                return await CompactSessionAsync(agent, sessionKey, argument).ConfigureAwait(false);

            case ChatCommand.SessionDisable:
                UpdateOverrides(agentName, o => o with { SessionEnabled = false });
                logger.LogInformation("{ClassName} session persistence disabled via slash command for {AgentName}",
                    nameof(AgentCommandHandler), agentName);
                return "Session persistence disabled. Each message will start a fresh conversation.";

            case ChatCommand.SessionEnable:
                UpdateOverrides(agentName, o => o with { SessionEnabled = true });
                logger.LogInformation("{ClassName} session persistence enabled via slash command for {AgentName}",
                    nameof(AgentCommandHandler), agentName);
                return "Session persistence enabled.";

            case ChatCommand.SessionSave:
                return await SaveSnapshotAsync(agent, agentName, sessionKey, argument).ConfigureAwait(false);

            case ChatCommand.SessionLoad:
                return await LoadSnapshotAsync(agent, agentName, sessionKey, argument).ConfigureAwait(false);

            case ChatCommand.SessionDelete:
                return await DeleteSnapshotAsync(agentName, argument).ConfigureAwait(false);

            case ChatCommand.Model:
                if (string.IsNullOrWhiteSpace(argument))
                    return $"Current model override: {GetModelOverride(agentName) ?? "(none — using provider default)"}";
                var modelOverride = UpdateOverrides(agentName, o => o with { Model = argument }).Model;
                logger.LogInformation("{ClassName} model overridden to {Model} via slash command for {AgentName}",
                    nameof(AgentCommandHandler), modelOverride, agentName);
                if (onModelChanged is not null)
                    await onModelChanged(argument).ConfigureAwait(false);
                return $"Model overridden to: {modelOverride}";

            case ChatCommand.Instructions:
                if (string.IsNullOrWhiteSpace(argument))
                {
                    var current = GetInstructionsOverride(agentName);
                    if (current is null)
                        return "Current instructions override: (none — using configured default)";
                    var preview = current.Length > 200
                        ? current[..200] + "..."
                        : current;
                    return $"Current instructions override ({current.Length} chars): {preview}";
                }
                var instructionsOverride = UpdateOverrides(agentName, o => o with { Instructions = argument }).Instructions!;
                logger.LogInformation("{ClassName} instructions overridden via slash command for {AgentName} ({Length} chars)",
                    nameof(AgentCommandHandler), agentName, instructionsOverride.Length);
                return $"Instructions overridden ({instructionsOverride.Length} chars).";

            default:
                return null;
        }
    }

    /// <summary>Loads the agent session from the store, or returns <see langword="null"/> for a new conversation.</summary>
    /// <remarks>Returns <see langword="null"/> immediately when <see cref="IsSessionEnabled"/> is <see langword="false"/> for this agent.</remarks>
    public async Task<AgentSession?> LoadSessionAsync(AIAgent agent, string agentName)
    {
        if (!IsSessionEnabled(agentName))
            return null;
        var sessionKey = BuildSessionKey(agentName);
        var json = await sessionStore.GetAsync(sessionKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        JsonElement reloaded = json.FromJson<JsonElement>(JsonSerializerOptions.Web)!;
        return await agent.DeserializeSessionAsync(reloaded, JsonSerializerOptions.Web).ConfigureAwait(false);
    }

    /// <summary>Persists the agent session to the store with a 7-day sliding expiration.</summary>
    /// <remarks>No-op when <see cref="IsSessionEnabled"/> is <see langword="false"/> for this agent.</remarks>
    public async Task SaveSessionAsync(AIAgent agent, string agentName, AgentSession session)
    {
        if (!IsSessionEnabled(agentName))
            return;
        var sessionKey = BuildSessionKey(agentName);
        var serialized = await agent.SerializeSessionAsync(session, JsonSerializerOptions.Web).ConfigureAwait(false);
        await sessionStore.SetAsync(sessionKey, serialized.ToJson(), _sessionTtl).ConfigureAwait(false);
    }

    /// <summary>Applies the model override for <paramref name="agentName"/> to <paramref name="chatOptions"/> when set.</summary>
    /// <param name="chatOptions">The options to mutate.</param>
    /// <param name="agentName">The agent display name (<see cref="AgentConfig.Name"/>).</param>
    public void ApplyModelOverride(ChatOptions chatOptions, string agentName)
    {
        var modelOverride = GetModelOverride(agentName);
        if (!string.IsNullOrWhiteSpace(modelOverride))
            chatOptions.ModelId = modelOverride;
    }

    /// <summary>Applies the instructions override for <paramref name="agentName"/> to <paramref name="chatOptions"/> when set, wrapping with shared prefix/suffix from <paramref name="aiConfig"/>.</summary>
    /// <param name="chatOptions">The options to mutate.</param>
    /// <param name="agentName">The agent display name (<see cref="AgentConfig.Name"/>).</param>
    /// <param name="aiConfig">Optional root AI configuration supplying shared instruction prefix/suffix.</param>
    public void ApplyInstructionsOverride(ChatOptions chatOptions, string agentName, AIConfig? aiConfig = null)
    {
        var instructionsOverride = GetInstructionsOverride(agentName);
        if (!string.IsNullOrWhiteSpace(instructionsOverride))
            chatOptions.Instructions = AgentExtensions.WrapInstructions(instructionsOverride, aiConfig);
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
    private async Task<string> BuildSessionInfoTextAsync(AIAgent agent, string agentName, string sessionKey)
    {
        var json = await sessionStore.GetAsync(sessionKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return "No active session.";
        var sizeBytes = Encoding.UTF8.GetByteCount(json);
        try
        {
            var sessionElement = json.FromJson<JsonElement>(JsonSerializerOptions.Web)!;
            var session = await agent.DeserializeSessionAsync(sessionElement, JsonSerializerOptions.Web).ConfigureAwait(false);
            var entries = ChatCommandParser.GetStateBagEntries(session);
            var lines = new StringBuilder();
            lines.AppendLine($"Session active (persistence: {(IsSessionEnabled(agentName) ? "on" : "off")}). Size: {sizeBytes:N0} bytes.");
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
        var json = await sessionStore.GetAsync(sessionKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return "No active session to compact.";
        try
        {
            var sessionElement = json.FromJson<JsonElement>(JsonSerializerOptions.Web)!;
            var session = await agent.DeserializeSessionAsync(sessionElement, JsonSerializerOptions.Web).ConfigureAwait(false);
            if (!ChatCommandParser.TryCompactSession(session, keepCount, out var removedCount))
                return "No in-memory chat history found in the current session.";
            var serialized = await agent.SerializeSessionAsync(session, JsonSerializerOptions.Web).ConfigureAwait(false);
            await sessionStore.SetAsync(sessionKey, serialized.ToJson(), _sessionTtl).ConfigureAwait(false);
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
        var json = await sessionStore.GetAsync(sessionKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return "No active session to save.";
        var snapshotKey = BuildSnapshotKey(agentName, snapshotName);
        await sessionStore.SetAsync(snapshotKey, json, _sessionTtl).ConfigureAwait(false);
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
        var json = await sessionStore.GetAsync(snapshotKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return $"No snapshot named \"{snapshotName}\" found.";
        await sessionStore.SetAsync(sessionKey, json, _sessionTtl).ConfigureAwait(false);
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
        await sessionStore.DeleteAsync(snapshotKey).ConfigureAwait(false);
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
