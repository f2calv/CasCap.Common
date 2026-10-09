namespace CasCap.Common.AI.Tests.Integration;

/// <summary>Environment-provided settings for local llama.cpp integration tests.</summary>
public sealed record LocalLlamaCppTestSettings
{
    /// <summary>Gets the OpenAI-compatible llama.cpp endpoint.</summary>
    public required Uri Endpoint { get; init; }

    /// <summary>Gets the loaded text model name.</summary>
    public required string ModelName { get; init; }

    /// <summary>Gets the optional multimodal model name.</summary>
    public string? VisionModelName { get; init; }

    /// <summary>Gets the optional ingress Basic-auth username.</summary>
    public string? BasicAuthUsername { get; init; }

    /// <summary>Gets the optional ingress Basic-auth password.</summary>
    public string? BasicAuthPassword { get; init; }

    /// <summary>Gets the request timeout.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Loads settings without persisting or exposing local endpoint details.</summary>
    public static bool TryLoad(out LocalLlamaCppTestSettings? settings)
    {
        var endpointValue = Environment.GetEnvironmentVariable("CASCAP_LOCAL_LLAMA_CPP_ENDPOINT");
        var modelName = Environment.GetEnvironmentVariable("CASCAP_LOCAL_LLAMA_CPP_MODEL");
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
            || string.IsNullOrWhiteSpace(modelName))
        {
            settings = null;
            return false;
        }

        var timeout = int.TryParse(
            Environment.GetEnvironmentVariable("CASCAP_LOCAL_LLAMA_CPP_TIMEOUT_SECONDS"),
            out var timeoutSeconds)
            && timeoutSeconds > 0
                ? TimeSpan.FromSeconds(timeoutSeconds)
                : TimeSpan.FromMinutes(2);

        settings = new LocalLlamaCppTestSettings
        {
            Endpoint = endpoint,
            ModelName = modelName,
            VisionModelName = Environment.GetEnvironmentVariable("CASCAP_LOCAL_LLAMA_CPP_VISION_MODEL"),
            BasicAuthUsername = Environment.GetEnvironmentVariable("CASCAP_LOCAL_LLAMA_CPP_BASIC_AUTH_USERNAME"),
            BasicAuthPassword = Environment.GetEnvironmentVariable("CASCAP_LOCAL_LLAMA_CPP_BASIC_AUTH_PASSWORD"),
            Timeout = timeout,
        };
        if (string.IsNullOrWhiteSpace(settings.BasicAuthUsername)
            != string.IsNullOrWhiteSpace(settings.BasicAuthPassword))
        {
            settings = null;
            return false;
        }
        return true;
    }
}