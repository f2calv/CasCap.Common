namespace CasCap.Common.Configuration.Aot.TestHost.Models;

/// <summary>Exercises complete snapshots, nested validation and cross-property invariants.</summary>
internal sealed class Controls
{
    /// <summary>Gets or sets the synthetic feature control.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the bounded lower value.</summary>
    public int Minimum { get; set; }

    /// <summary>Gets or sets the bounded upper value.</summary>
    public int Maximum { get; set; } = 5;

    /// <summary>Gets or sets an enum requiring explicit bounds validation.</summary>
    public DayOfWeek Day { get; set; }

    /// <summary>Gets or sets the complete child collection.</summary>
    public ChildControls[] Children { get; set; } = [];
}
