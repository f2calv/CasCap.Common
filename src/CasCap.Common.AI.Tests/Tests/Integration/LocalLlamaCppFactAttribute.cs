namespace CasCap.Common.AI.Tests.Integration;

/// <summary>Runs a test only when local llama.cpp integration testing is explicitly enabled.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalLlamaCppFactAttribute : FactAttribute
{
    /// <summary>Initializes a local-only llama.cpp test.</summary>
    public LocalLlamaCppFactAttribute(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = "Local llama.cpp tests require CASCAP_RUN_LOCAL_LLAMA_CPP_TESTS=true outside CI.";
        SkipUnless = nameof(LocalLlamaCppTestEnvironment.IsEnabled);
        SkipType = typeof(LocalLlamaCppTestEnvironment);
    }
}