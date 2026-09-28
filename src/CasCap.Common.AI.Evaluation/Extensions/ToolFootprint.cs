namespace CasCap.Common.Extensions;

/// <summary>Measures how much of every model request a tool definition occupies.</summary>
public static class ToolFootprint
{
    /// <summary>Characters in a tool's name, description and parameter schema.</summary>
    /// <param name="tool">The tool definition.</param>
    /// <remarks>
    /// Providers render definitions slightly differently, so treat this as a comparative measure. Divide by
    /// four for a rough English token estimate.
    /// </remarks>
    public static int Characters(AITool tool) => tool switch
    {
        AIFunctionDeclaration function => function.Name.Length + (function.Description?.Length ?? 0)
            + function.JsonSchema.GetRawText().Length,
        _ => tool.Name.Length + (tool.Description?.Length ?? 0),
    };

    /// <summary>Characters in every tool definition of a request.</summary>
    /// <param name="tools">The tool definitions.</param>
    public static int Characters(IEnumerable<AITool>? tools) => tools?.Sum(Characters) ?? 0;

    /// <summary>A rough English token estimate for a character count.</summary>
    /// <param name="characters">The character count.</param>
    public static int ApproximateTokens(int characters) => (characters + 3) / 4;
}
