namespace CasCap.Common.Extensions;

/// <summary>Extension methods for <see cref="DateTime"/> truncation and rounding.</summary>
public static class DateTimeExtensions
{
    /// <summary>Truncates a <see cref="DateTime"/> to the start of its hour.</summary>
    public static DateTime TruncateToHour(this DateTime dt) => new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Kind);

    /// <summary>Truncates a <see cref="DateTime"/> to the start of its day (midnight).</summary>
    public static DateTime TruncateToDay(this DateTime dt) => new(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Kind);

    /// <summary>Truncates a <see cref="DateTime"/> to the 1st of its month at midnight.</summary>
    public static DateTime TruncateToMonth(this DateTime dt) => new(dt.Year, dt.Month, 1, 0, 0, 0, dt.Kind);
}
