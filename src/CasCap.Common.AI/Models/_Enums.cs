namespace CasCap.Common.Models;

/// <summary>
/// The AI provider type (e.g. Ollama, OpenAI).
/// </summary>
public enum AgentType
{
    /// <summary>
    /// <see href="https://learn.microsoft.com/en-us/agent-framework/agents/providers/azure-openai?pivots=programming-language-csharp"/>
    /// </summary>
    AzureOpenAI = 1,
    /// <summary>
    /// <see href="https://learn.microsoft.com/en-us/agent-framework/agents/providers/azure-ai-foundry?pivots=programming-language-csharp"/>
    /// </summary>
    /// <remarks>
    /// TODO: not implemented — <see cref="CasCap.Common.Extensions.AgentExtensions.CreateAgent"/>
    /// throws <see cref="NotSupportedException"/> for this value. Implement via the
    /// <c>Microsoft.Agents.AI.Foundry</c> package, or remove this member so it cannot be
    /// selected in configuration. Deferred because that package is still preview
    /// (1.21.0-preview as of 2026-09-11).
    /// See https://www.nuget.org/packages/Microsoft.Agents.AI.Foundry
    /// </remarks>
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
