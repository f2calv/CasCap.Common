using System.Globalization;
using System.Text;

namespace CasCap.Common.Extensions;

/// <summary>Extension methods for <see cref="DateTime"/> truncation, formatting and arithmetic.</summary>
public static class DateTimeExtensions
{
    /// <summary>Truncates a <see cref="DateTime"/> to the start of its hour.</summary>
    public static DateTime TruncateToHour(this DateTime dt) => new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Kind);

    /// <summary>Truncates a <see cref="DateTime"/> to the start of its day (midnight).</summary>
    public static DateTime TruncateToDay(this DateTime dt) => new(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Kind);

    /// <summary>Truncates a <see cref="DateTime"/> to the 1st of its month at midnight.</summary>
    public static DateTime TruncateToMonth(this DateTime dt) => new(dt.Year, dt.Month, 1, 0, 0, 0, dt.Kind);

    /// <summary>truncate milliseconds off a .net datetime</summary>
    public static DateTime Truncate(this DateTime dateTime, TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero) return dateTime; // Or could throw an ArgumentException
        return dateTime.AddTicks(-(dateTime.Ticks % timeSpan.Ticks));
    }

    /// <summary>Returns all dates between the start and end date (exclusive of start, inclusive of end).</summary>
    public static List<DateTime> GetMissingDates(this DateTime dtStart, DateTime dtEnd)
    {
        //TODO: plug in known holidays dates somehow?
        var days = dtEnd.Date.Subtract(dtStart).Days;
        var missingDates = Enumerable.Range(1, days).Select(p => dtStart.AddDays(p)).ToArray();
        return missingDates.ToList();
    }

    /// <summary>Returns the number of seconds remaining until midnight (UTC).</summary>
    public static int SecondsTillMidnight(this DateTime dt)
        => dt.SecondsTillMidnight(DateTime.UtcNow);

    /// <summary>Returns the number of seconds remaining until midnight relative to the specified time.</summary>
    public static int SecondsTillMidnight(this DateTime dt, DateTime now)
    {
        var ts = dt.Date.AddDays(1) - now;
        return (int)ts.TotalSeconds;//does this round-up?
    }

    /// <summary>Returns a human-readable string representing the time difference between two dates.</summary>
    public static string GetTimeDifference(this DateTime dtiStart, DateTime dtiEnd,
        bool includeSeconds = true, bool includeMinutes = true, bool includeHours = true, bool includeDays = true, bool includeMilliseconds = false)
    {
        var ts = dtiStart.Subtract(dtiEnd).Duration();
        return ts.GetTimeDifference(includeSeconds, includeMinutes, includeHours, includeDays, includeMilliseconds);
    }

    /// <summary>Returns a human-readable string representing the specified <see cref="TimeSpan"/>.</summary>
    public static string GetTimeDifference(this TimeSpan ts,
        bool includeSeconds = true, bool includeMinutes = true, bool includeHours = true, bool includeDays = true, bool includeMilliseconds = false)
    {
        var sb = new StringBuilder();
        if (includeDays && ts.Days != 0) sb.Append(ts.Days + "d ");
        if (includeHours && ts.Hours != 0) sb.Append(ts.Hours + "h ");
        if (includeMinutes)
            if (ts.Minutes >= 1)
                sb.Append(ts.Minutes + "m ");
            else if (!includeSeconds)
                sb.Append("<1m");
        if (includeSeconds && ts.Seconds != 0) sb.Append(ts.Seconds + "s ");
        if (includeMilliseconds && ts.Milliseconds != 0) sb.Append(ts.Milliseconds + "ms ");
        return sb.ToString().Trim();
    }

    /// <summary>Represent a date in "yyyy-MM-dd" format</summary>
    public static string To_yyyy_MM_dd(this DateTime thisDateTime) => thisDateTime.ToString("yyyy-MM-dd");

#if NET8_0_OR_GREATER
    /// <summary>Converts a Unix timestamp in seconds to a <see cref="DateTime"/>.</summary>
    public static DateTime FromUnixTime(this long seconds) => DateTime.UnixEpoch.AddSeconds(seconds);

    /// <summary>Converts a Unix timestamp in milliseconds to a <see cref="DateTime"/>.</summary>
    public static DateTime FromUnixTimeMs(this long milliseconds) => DateTime.UnixEpoch.AddMilliseconds(milliseconds);

    /// <summary>
    /// Converts a Unix timestamp in milliseconds (as <see cref="double"/>) to a <see cref="DateTime"/>.
    /// </summary>
    public static DateTime FromUnixTimeMs(this double milliseconds) => DateTime.UnixEpoch.AddMilliseconds(milliseconds);
#endif

    /// <summary>Converts a <see cref="DateTime"/> to a Unix timestamp in seconds.</summary>
    public static long ToUnixTime(this DateTime dt) => ((DateTimeOffset)dt).ToUnixTimeSeconds();

    /// <summary>Converts a <see cref="DateTime"/> to a Unix timestamp in milliseconds.</summary>
    public static long ToUnixTimeMs(this DateTime dt) => dt.ToUnixTime() * 1000;

    /// <summary>Determines whether the specified date falls on a weekend.</summary>
    public static bool IsWeekend(this DateTime date) => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

    /// <summary>Determines whether the specified date falls on a weekday.</summary>
    public static bool IsWeekday(this DateTime date) => !date.IsWeekend();

    /// <summary>Sets a <see cref="DateTime"/> to be <see cref="DateTimeKind.Utc"/>.</summary>
    public static DateTime ToUtc(this DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    /// <summary>Formats a <see cref="DateTime"/> as a time string if today, otherwise as a date string.</summary>
    public static string ToDateOrTime(this DateTime thisDateTime, string dateFormat = "yyyy-MM-dd", string timeFormat = "HH:mm:ss")
    {
        return thisDateTime.ToString(thisDateTime.Date == DateTime.UtcNow.Date ? timeFormat : dateFormat);
    }

    /// <summary>Returns the first day of the week containing the specified date.</summary>
    public static DateTime FirstDayOfWeek(this DateTime dt, DayOfWeek startOfWeek)
    {
        var diff = dt.DayOfWeek - startOfWeek;
        if (diff < 0) diff += 7;
        return dt.AddDays(-1 * diff).Date;
    }

    /// <summary>Adds the specified number of weekdays (skipping weekends) to the date.</summary>
    public static DateTime AddWeekdays(this DateTime date, int days)
    {
        var sign = days < 0 ? -1 : 1;
        var unsignedDays = Math.Abs(days);
        var weekdaysAdded = 0;
        while (weekdaysAdded < unsignedDays)
        {
            date = date.AddDays(sign);
            if (date.IsWeekday())
                weekdaysAdded++;
        }
        return date;
    }

    /// <summary>Returns the first day of the month for the specified date.</summary>
    public static DateTime FirstDayOfMonth(this DateTime date, DateTimeKind kind = DateTimeKind.Utc)
        => new(date.Year, date.Month, 1, 0, 0, 0, kind);

    /// <summary>Returns the last day of the month for the specified date.</summary>
    public static DateTime LastDayOfMonth(this DateTime date, DateTimeKind kind = DateTimeKind.Utc)
        => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 0, 0, 0, kind);

    /// <summary>Returns the last day of the year for the specified date.</summary>
    public static DateTime LastDayOfYear(this DateTime date, DateTimeKind kind = DateTimeKind.Utc)
        => new DateTime(date.Year, 12, 1, 0, 0, 0, kind).LastDayOfMonth();

    /// <summary>Returns the absolute difference in months between two dates.</summary>
    public static int MonthDifference(this DateTime lValue, DateTime rValue)
        => Math.Abs(lValue.Month - rValue.Month + 12 * (lValue.Year - rValue.Year));

    /// <summary>
    /// Converts a nullable <see cref="DateTime"/> to its string representation using current culture info.
    /// </summary>
    public static string ToString(this DateTime? date)
        => date.ToString(DateTimeFormatInfo.CurrentInfo);

    /// <summary>
    /// Converts a nullable <see cref="DateTime"/> to its string representation using the specified format.
    /// </summary>
    public static string ToString(this DateTime? date, string format)
        => date.ToString(format, DateTimeFormatInfo.CurrentInfo);

    /// <summary>
    /// Converts a nullable <see cref="DateTime"/> to its string representation using the specified provider.
    /// </summary>
    public static string ToString(this DateTime? date, IFormatProvider provider)
    {
        if (date.HasValue)
            return date.Value.ToString(provider);
        return string.Empty;
    }

    /// <summary>
    /// Converts a nullable <see cref="DateTime"/> to its string representation using the specified format and provider.
    /// </summary>
    public static string ToString(this DateTime? date, string format, IFormatProvider provider)
    {
        if (date.HasValue)
            return date.Value.ToString(format, provider);
        else
            return string.Empty;
    }

    /// <summary>Returns a human-readable relative date string (e.g. "2 days ago").</summary>
    public static string ToRelativeDateString(this DateTime date) => GetRelativeDateValue(date, DateTime.UtcNow);

    /// <summary>Returns a human-readable relative date string compared to <see cref="DateTime.UtcNow"/>.</summary>
    public static string ToRelativeDateStringUtc(this DateTime date) => GetRelativeDateValue(date, DateTime.UtcNow);

    /// <summary>Generates a sequence of consecutive dates starting from the specified date.</summary>
    public static IEnumerable<DateTime> ToArray(this DateTime input, int length = 1)
    {
        length = length > 0 ? length : 1;
        return Enumerable.Range(0, length).Select(a => input.AddDays(a));
    }

    #region private/static helpers

    private static string GetRelativeDateValue(DateTime date, DateTime comparedTo)
    {
        TimeSpan ts = comparedTo.Subtract(date);
        if (ts.TotalDays >= 365)
            return string.Concat("on ", date.ToString("MMMM d, yyyy"));
        if (ts.TotalDays >= 7)
            return string.Concat("on ", date.ToString("MMMM d"));
        else if (ts.TotalDays > 1)
            return string.Format("{0:N0} days ago", ts.TotalDays);
        else if (ts.TotalDays == 1)
            return "yesterday";
        else if (ts.TotalHours >= 2)
            return string.Format("{0:N0} hours ago", ts.TotalHours);
        else if (ts.TotalMinutes >= 60)
            return "more than an hour ago";
        else if (ts.TotalMinutes >= 5)
            return string.Format("{0:N0} minutes ago", ts.TotalMinutes);
        if (ts.TotalMinutes >= 1)
            return "a few minutes ago";
        else
            return "less than a minute ago";
    }

    #endregion
}
