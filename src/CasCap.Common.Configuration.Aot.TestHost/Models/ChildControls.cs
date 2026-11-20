namespace CasCap.Common.Configuration.Aot.TestHost.Models;

/// <summary>Exercises nested values and complete collection replacement.</summary>
internal sealed class ChildControls
{
    /// <summary>Gets or sets a bounded child count.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets a synthetic payload used for the byte limit.</summary>
    public string Label { get; set; } = "";
}
