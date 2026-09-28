namespace CasCap.Common.Models;

/// <summary>How the evaluation harness treats a tool the model calls.</summary>
public enum ToolDisposition
{
    /// <summary>Reads state; the scenario fixture supplies the response.</summary>
    Query,

    /// <summary>Changes state or sends a message; the harness records the call and never executes it.</summary>
    SideEffect,

    /// <summary>Delegates to a sub-agent, which the harness runs with the same fixtures and recorder.</summary>
    Delegation,
}
