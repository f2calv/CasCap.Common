namespace CasCap.Models;

/// <summary>
/// Slash-commands that can be entered at a console prompt or sent via a messaging interface
/// to control session behaviour rather than forwarding input to the AI agent.
/// </summary>
/// <remarks>
/// Each member carries a <see cref="DescriptionAttribute"/> whose value is the exact command
/// prefix the user must type. Arguments, where applicable, follow the prefix separated by a space.
/// </remarks>
public enum ChatCommand
{
    /// <summary>Prints a list of all available commands and their descriptions.</summary>
    [Description("/help")]
    Help,

    /// <summary>Displays technical information about the current session state (size in bytes, message count, StateBag keys).</summary>
    [Description("/session info")]
    SessionInfo,

    /// <summary>Discards the current session so the next prompt starts a fresh conversation.</summary>
    [Description("/session reset")]
    SessionReset,

    /// <summary>Sends a one-off prompt to the agent bypassing (and without modifying) the active session. Usage: /session bypass {prompt}</summary>
    [Description("/session bypass")]
    SessionBypass,

    /// <summary>Reduces the active session to the newest N messages, discarding older history. Usage: /session compact {count}</summary>
    [Description("/session compact")]
    SessionCompact,

    /// <summary>Disables session persistence; each message starts a fresh conversation until re-enabled.</summary>
    [Description("/session disable")]
    SessionDisable,

    /// <summary>Re-enables session persistence after a previous <see cref="SessionDisable"/>.</summary>
    [Description("/session enable")]
    SessionEnable,

    /// <summary>Saves a named snapshot of the active session for later analysis. Usage: /session save {name}</summary>
    [Description("/session save")]
    SessionSave,

    /// <summary>Loads a previously saved named snapshot into the active session. Usage: /session load {name}</summary>
    [Description("/session load")]
    SessionLoad,

    /// <summary>Deletes a previously saved named session snapshot. Usage: /session delete {name}</summary>
    [Description("/session delete")]
    SessionDelete,

    /// <summary>Overrides the model used for subsequent requests in the current session. Usage: /model {modelName}</summary>
    [Description("/model")]
    Model,

    /// <summary>Replaces the system instructions for subsequent requests. Usage: /instructions {text}</summary>
    [Description("/instructions")]
    Instructions,
}

/// <summary>
/// The AI provider type (e.g. Ollama, OpenAI).
/// </summary>
public enum AgentType
{
    /// <summary>No agent type specified.</summary>
    None,
    /// <summary>
    /// <see href="https://learn.microsoft.com/en-us/agent-framework/agents/providers/azure-openai?pivots=programming-language-csharp"/>
    /// </summary>
    AzureOpenAI,
    /// <summary>
    /// <see href="https://learn.microsoft.com/en-us/agent-framework/agents/providers/azure-ai-foundry?pivots=programming-language-csharp"/>
    /// </summary>
    AzureAIFoundry,
    /// <summary>
    /// <see href="https://learn.microsoft.com/en-us/agent-framework/agents/providers/ollama?pivots=programming-language-csharp"/>
    /// </summary>
    Ollama,
    /// <summary>
    /// <see href="https://learn.microsoft.com/en-us/agent-framework/agents/providers/openai?pivots=programming-language-csharp"/>
    /// </summary>
    OpenAI
}
