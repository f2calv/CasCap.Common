namespace CasCap.Common.Services;

/// <summary>
/// Wraps a real application tool so the model sees its production name and schema while the harness controls
/// what it returns and records every call.
/// </summary>
/// <remarks>
/// Query tools return the scenario fixture. Side-effect tools are never executed: the call is recorded and a
/// sandbox acknowledgement returned. Delegation tools run the sub-agent, which the harness built with the
/// same fixtures and recorder.
/// </remarks>
/// <param name="innerFunction">The real tool, whose schema and serializer options are preserved.</param>
/// <param name="agentKey">The agent offered the tool.</param>
/// <param name="disposition">How the harness treats a call.</param>
/// <param name="responder">The scenario fixture, or <see langword="null"/> when the scenario did not anticipate the tool.</param>
/// <param name="recorder">The run's recorder.</param>
/// <param name="descriptionOverride">A variant description, or <see langword="null"/> to keep the shipped one.</param>
/// <param name="schemaOverride">A variant parameter schema, or <see langword="null"/> to keep the shipped one.</param>
public sealed class FixtureToolFunction(
    AIFunction innerFunction,
    string agentKey,
    ToolDisposition disposition,
    Func<AIFunctionArguments, object?>? responder,
    EvaluationRecorder recorder,
    string? descriptionOverride = null,
    JsonElement? schemaOverride = null)
    : DelegatingAIFunction(innerFunction)
{
    /// <summary>Returned in place of executing a side-effect tool.</summary>
    public static readonly IReadOnlyDictionary<string, object> SandboxResponse = new Dictionary<string, object>
    {
        ["accepted"] = false,
        ["reason"] = "Evaluation sandbox: this action was recorded but not performed.",
    };

    /// <summary>Returned when the scenario supplies no fixture for a query tool.</summary>
    public static readonly IReadOnlyDictionary<string, object> MissingFixtureResponse = new Dictionary<string, object>
    {
        ["error"] = "No data is available from this tool.",
    };

    /// <summary>How the harness treats a call.</summary>
    public ToolDisposition Disposition => disposition;

    /// <inheritdoc/>
    public override string Description => descriptionOverride ?? base.Description;

    /// <inheritdoc/>
    public override JsonElement JsonSchema => schemaOverride ?? base.JsonSchema;

    /// <inheritdoc/>
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var argumentsJson = JsonSerializer.Serialize(
            arguments.ToDictionary(a => a.Key, a => a.Value), JsonSerializerOptions);
        recorder.Add(new RecordedToolCall(agentKey, Name, disposition, argumentsJson,
            disposition is not ToolDisposition.Query || responder is not null));

        return disposition switch
        {
            ToolDisposition.Delegation => await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false),
            ToolDisposition.SideEffect => JsonSerializer.SerializeToElement(SandboxResponse, JsonSerializerOptions),
            _ => JsonSerializer.SerializeToElement(responder is null ? MissingFixtureResponse : responder(arguments),
                JsonSerializerOptions),
        };
    }
}
