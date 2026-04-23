namespace CasCap.Models;

/// <summary>
/// Infrastructure configuration for an AI provider (connection details, model, auth).
/// Multiple providers can be defined in <see cref="AIConfig.Providers"/>.
/// </summary>
public record ProviderConfig
{
    /// <inheritdoc cref="AgentType"/>
    [Required]
    public required AgentType Type { get; init; } = AgentType.Ollama;

    /// <summary>
    /// The endpoint URI for the AI provider. Required for <see cref="AgentType.Ollama"/>,
    /// optional for <see cref="AgentType.OpenAI"/> (the SDK uses its default endpoint).
    /// </summary>
    public Uri? Endpoint { get; init; }

    /// <summary>
    /// The model name to use (e.g. "granite3.2-vision", "qwen2.5vl:7b").
    /// </summary>
    [Required, MinLength(1)]
    public required string ModelName { get; init; }

    /// <summary>
    /// Reasoning effort for this provider's model. When <see langword="null"/> no reasoning options are set.
    /// </summary>
    public ReasoningEffort? ReasoningEffort { get; init; }

    /// <summary>
    /// API key for providers that require key-based authentication (e.g. <see cref="AgentType.OpenAI"/>).
    /// Should be populated via user secrets or Key Vault rather than stored in plain-text configuration.
    /// </summary>
    public string? ApiKey { get; init; }
}
