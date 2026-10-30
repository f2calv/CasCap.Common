using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CasCap.Common.Configuration.Blazor.Tests.Models;

public sealed record EditorControls
{
    [Range(0, 5)]
    public int Verbosity { get; set; } = 2;

    public bool FeatureEnabled { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DayOfWeek Day { get; set; } = DayOfWeek.Monday;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);
}
