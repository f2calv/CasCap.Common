namespace CasCap.Models;

/// <summary>MCP-friendly projection of <see cref="ProviderConfig"/> (excludes sensitive fields).</summary>
public record ProviderInfo
{
    /// <summary>Dictionary key used in <see cref="AIConfig.Providers"/>.</summary>
    [Description("Unique provider key identifying the provider in AIConfig.Providers.")]
    public required string Key { get; init; }

    /// <summary>The AI provider type.</summary>
    [Description("Provider type. Values: Ollama, AzureOpenAI, AzureAIFoundry, OpenAI.")]
    public required string Type { get; init; }

    /// <summary>The model name configured for this provider.</summary>
    [Description("Model name, e.g. granite3.2-vision, gpt-4o.")]
    public required string ModelName { get; init; }

    /// <summary>The endpoint URI (null for cloud providers using default endpoints).</summary>
    [Description("Endpoint URI, or null when the SDK default is used.")]
    public string? Endpoint { get; init; }

    /// <summary>Reasoning effort level, if configured.</summary>
    [Description("Reasoning effort level (Low, Medium, High), or null if not set.")]
    public string? ReasoningEffort { get; init; }
}
