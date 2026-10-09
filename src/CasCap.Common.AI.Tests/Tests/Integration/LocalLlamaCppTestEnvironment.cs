namespace CasCap.Common.AI.Tests.Integration;

/// <summary>Controls whether local llama.cpp integration tests may execute.</summary>
public static class LocalLlamaCppTestEnvironment
{
    /// <summary>Gets whether tests were explicitly enabled outside CI.</summary>
    public static bool IsEnabled =>
        !CIEnvironment.IsCI
        && bool.TryParse(Environment.GetEnvironmentVariable("CASCAP_RUN_LOCAL_LLAMA_CPP_TESTS"), out var enabled)
        && enabled;
}