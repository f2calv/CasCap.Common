namespace CasCap.Common.Configuration.TestHost.Models;

/// <summary>Observes options and retained process state through the HTTP boundary.</summary>
public sealed record HostObservation
{
    /// <summary>Gets the host's retained singleton sentinel.</summary>
    public required Guid Sentinel { get; init; }

    /// <summary>Gets the observed complete options snapshot.</summary>
    public required RuntimeControls Controls { get; init; }
}
