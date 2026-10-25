using System.ComponentModel.DataAnnotations;

namespace CasCap.Common.Configuration.TestHost.Models;

/// <summary>Small typed contract used to observe complete monitor snapshots.</summary>
public sealed class RuntimeControls
{
    /// <summary>Gets or sets a bounded verbosity control.</summary>
    [Range(0, 5)]
    public int Verbosity { get; set; } = 2;

    /// <summary>Gets or sets the complete category selection.</summary>
    public string[] Categories { get; set; } = [];
}
