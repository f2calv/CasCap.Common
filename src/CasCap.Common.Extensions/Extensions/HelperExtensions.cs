using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Serialization;

namespace CasCap.Common.Extensions;

/// <summary>
/// General-purpose extension methods for collections, dates and more.
/// </summary>
public static class HelperExtensions
{
    /// <summary>
    /// Checks if the current host environment name is 'Integration'.
    /// </summary>
    public static bool IsIntegration(this IHostEnvironment env) => env.IsEnvironment("Integration");

    /// <summary>
    /// Checks if the current host environment name is 'Test'.
    /// </summary>
    public static bool IsTest(this IHostEnvironment env) => env.IsEnvironment("Test");

    #region IsNullOrEmpty & IsNullOrWhiteSpace cannot interpret nullable reference types correctly, needs more research
    //https://github.com/dotnet/roslyn/issues/37995
    //https://github.com/JamesNK/Newtonsoft.Json/pull/2163/commits/fba64bcf9b8f41500da1c1dd75825f3db99cd3b4
    //public static bool IsNullOrWhiteSpace(this string? val)
    //{
    //    return val is null || val.Trim() == string.Empty;
    //    //return string.IsNullOrWhiteSpace(value);
    //}

    //public static bool IsNullOrEmpty([NotNullWhen(false)] string? value)
    //{
    //    return string.IsNullOrEmpty(value);
    //}

    //public static bool IsNullOrEmpty(string? value)//conflicts with collections extension IsNullOrEmpty
    //{
    //    return string.IsNullOrEmpty(value);
    //    //return input?.Length > 0;
    //}
    #endregion

    /// <summary>
    /// Deserializes an XML string into an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="input">The XML string.</param>
    /// <returns>The deserialized object, or <see langword="null"/> if deserialization fails.</returns>
    public static T? FromXml<T>(this string input) where T : class
    {
        var ser = new XmlSerializer(typeof(T));
        using var sr = new StringReader(input);
        return (T?)ser.Deserialize(sr);
    }

    /// <summary>
    /// Deserializes a byte array containing XML into an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="bytes">The byte array containing XML data.</param>
    /// <returns>The deserialized object, or <see langword="null"/> if deserialization fails.</returns>
    public static T? FromBytes<T>(this byte[] bytes) where T : class
    {
        var ser = new XmlSerializer(typeof(T));
        using var ms = new MemoryStream(bytes);
        return (T?)ser.Deserialize(ms);
    }

    /// <summary>
    /// Splits a list into batches of the specified size.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="objects">The source list.</param>
    /// <param name="batchSize">The maximum number of items per batch.</param>
    /// <returns>A dictionary keyed by batch number.</returns>
    public static Dictionary<int, List<T>> GetBatches<T>(this List<T> objects, int batchSize)
    {
        var batches = new Dictionary<int, List<T>>();
        for (var i = 0; i < objects.Count; i++)
        {
            var batchNumber = i / batchSize;
            if (!batches.ContainsKey(batchNumber))
                batches.Add(batchNumber, []);
            batches[batchNumber].Add(objects[i]);
        }
        return batches;
    }

    /// <summary>
    /// Converts a <see cref="Dictionary{TKey, TValue}"/> to a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </summary>
    public static ConcurrentDictionary<T, V> ToConcurrentDictionary<T, V>(this Dictionary<T, V> d2) where T : notnull
    {
        var d1 = new ConcurrentDictionary<T, V>();
        foreach (var z in d2)
        {
            if (!d1.TryAdd(z.Key, z.Value))
                throw new GenericException($"AddRange failed due to conflicting key");
        }
        return d1;
    }

    /// <summary>
    /// Adds all entries from a <see cref="Dictionary{TKey, TValue}"/> to a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </summary>
    public static ConcurrentDictionary<T, V> AddRange<T, V>(this ConcurrentDictionary<T, V> d1, Dictionary<T, V> d2) where T : notnull
    {
        foreach (var z in d2)
        {
            if (!d1.TryAdd(z.Key, z.Value))
                throw new GenericException("AddRange failed due to conflicting key");
        }
        return d1;
    }

    /// <summary>
    /// Adds all entries from one <see cref="Dictionary{TKey, TValue}"/> to another.
    /// </summary>
    public static Dictionary<T, V> AddRange<T, V>(this Dictionary<T, V> d1, Dictionary<T, V> d2) where T : notnull
    {
        foreach (var z in d2)
            d1.Add(z.Key, z.Value);
        return d1;
    }

    /// <summary>
    /// Adds all elements from a <see cref="List{T}"/> to the <see cref="HashSet{T}"/>.
    /// </summary>
    public static HashSet<T> AddRange<T>(this HashSet<T> hs, List<T> l)
    {
        foreach (var z in l)
            hs.Add(z);
        return hs;
    }

    /// <summary>
    /// Adds all elements from an <see cref="IEnumerable{T}"/> to the <see cref="HashSet{T}"/>.
    /// </summary>
    public static HashSet<T> AddRange<T>(this HashSet<T> hs, IEnumerable<T> l)
    {
        foreach (var z in l)
            hs.Add(z);
        return hs;
    }

    /// <summary>
    /// Adds all elements from another <see cref="HashSet{T}"/> to the <see cref="HashSet{T}"/>.
    /// </summary>
    public static HashSet<T> AddRange<T>(this HashSet<T> hs, HashSet<T> l)
    {
        foreach (var z in l)
            hs.Add(z);
        return hs;
    }

    /// <summary>
    /// Converts an <see cref="IEnumerable{T}"/> to a <see cref="HashSet{T}"/>.
    /// </summary>
    public static HashSet<T> ToHashSet<T>(this IEnumerable<T> l)//can remove if we use .net standard 2.1?
    {
        var hs = new HashSet<T>();
        foreach (var z in l)
            hs.Add(z);
        return hs;
    }

    /// <summary>
    /// Returns all dates between the start and end date (exclusive of start, inclusive of end).
    /// </summary>
    public static List<DateTime> GetMissingDates(this DateTime dtStart, DateTime dtEnd)
    {
        //TODO: plug in known holidays dates somehow?
        var days = dtEnd.Date.Subtract(dtStart).Days;
        var missingDates = Enumerable.Range(1, days).Select(p => dtStart.AddDays(p)).ToArray();
        return missingDates.ToList();
    }

    /// <summary>
    /// Checks if a struct has been instantiated.
    /// </summary>
    public static bool IsNull<T>(this T source) where T : struct => source.Equals(default(T));

    /// <summary>
    /// Returns the number of seconds remaining until midnight (UTC).
    /// </summary>
    public static int SecondsTillMidnight(this DateTime dt)
        => dt.SecondsTillMidnight(DateTime.UtcNow);
    /// <summary>
    /// Returns the number of seconds remaining until midnight relative to the specified time.
    /// </summary>
    public static int SecondsTillMidnight(this DateTime dt, DateTime now)
    {
        var ts = dt.Date.AddDays(1) - now;
        return (int)ts.TotalSeconds;//does this round-up?
    }

    /// <summary>
    /// Returns a human-readable string representing the time difference between two dates.
    /// </summary>
    public static string GetTimeDifference(this DateTime dtiStart, DateTime dtiEnd,
        bool includeSeconds = true, bool includeMinutes = true, bool includeHours = true, bool includeDays = true, bool includeMilliseconds = false)
    {
        var ts = dtiStart.Subtract(dtiEnd).Duration();
        return ts.GetTimeDifference(includeSeconds, includeMinutes, includeHours, includeDays, includeMilliseconds);
    }

    /// <summary>
    /// Returns a human-readable string representing the specified <see cref="TimeSpan"/>.
    /// </summary>
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

    /// <summary>
    /// Represent a date in "yyyy-MM-dd" format
    /// </summary>
    public static string To_yyyy_MM_dd(this DateTime thisDateTime) => thisDateTime.ToString("yyyy-MM-dd");

#if NET8_0_OR_GREATER
    /// <summary>
    /// Converts a Unix timestamp in seconds to a <see cref="DateTime"/>.
    /// </summary>
    public static DateTime FromUnixTime(this long seconds) => DateTime.UnixEpoch.AddSeconds(seconds);

    /// <summary>
    /// Converts a Unix timestamp in milliseconds to a <see cref="DateTime"/>.
    /// </summary>
    public static DateTime FromUnixTimeMs(this long milliseconds) => DateTime.UnixEpoch.AddMilliseconds(milliseconds);

    /// <summary>
    /// Converts a Unix timestamp in milliseconds (as <see cref="double"/>) to a <see cref="DateTime"/>.
    /// </summary>
    public static DateTime FromUnixTimeMs(this double milliseconds) => DateTime.UnixEpoch.AddMilliseconds(milliseconds);
#endif

    /// <summary>
    /// Converts a <see cref="DateTime"/> to a Unix timestamp in seconds.
    /// </summary>
    public static long ToUnixTime(this DateTime dt) => ((DateTimeOffset)dt).ToUnixTimeSeconds();

    /// <summary>
    /// Converts a <see cref="DateTime"/> to a Unix timestamp in milliseconds.
    /// </summary>
    public static long ToUnixTimeMs(this DateTime dt) => dt.ToUnixTime() * 1000;

    /// <summary>
    /// Determines whether the specified date falls on a weekend.
    /// </summary>
    public static bool IsWeekend(this DateTime date) => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

    /// <summary>
    /// Determines whether the specified date falls on a weekday.
    /// </summary>
    public static bool IsWeekday(this DateTime date) => !date.IsWeekend();

    /// <summary>
    /// Sets a <see cref="DateTime"/> to be <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    public static DateTime ToUtc(this DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    /// <summary>
    /// Formats a <see cref="DateTime"/> as a time string if today, otherwise as a date string.
    /// </summary>
    public static string ToDateOrTime(this DateTime thisDateTime, string dateFormat = "yyyy-MM-dd", string timeFormat = "HH:mm:ss")
    {
        return thisDateTime.ToString(thisDateTime.Date == DateTime.UtcNow.Date ? timeFormat : dateFormat);
    }

    /// <summary>
    /// truncate milliseconds off a .net datetime
    /// </summary>
    public static DateTime Truncate(this DateTime dateTime, TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero) return dateTime; // Or could throw an ArgumentException
        return dateTime.AddTicks(-(dateTime.Ticks % timeSpan.Ticks));
    }

    /// <summary>
    /// Returns the first day of the week containing the specified date.
    /// </summary>
    public static DateTime FirstDayOfWeek(this DateTime dt, DayOfWeek startOfWeek)
    {
        var diff = dt.DayOfWeek - startOfWeek;
        if (diff < 0) diff += 7;
        return dt.AddDays(-1 * diff).Date;
    }

    /// <summary>
    /// Adds the specified number of weekdays (skipping weekends) to the date.
    /// </summary>
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

    /// <summary>
    /// Returns the first day of the month for the specified date.
    /// </summary>
    public static DateTime FirstDayOfMonth(this DateTime date, DateTimeKind kind = DateTimeKind.Utc)
        => new(date.Year, date.Month, 1, 0, 0, 0, kind);

    /// <summary>
    /// Returns the last day of the month for the specified date.
    /// </summary>
    public static DateTime LastDayOfMonth(this DateTime date, DateTimeKind kind = DateTimeKind.Utc)
        => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 0, 0, 0, kind);

    /// <summary>
    /// Returns the last day of the year for the specified date.
    /// </summary>
    public static DateTime LastDayOfYear(this DateTime date, DateTimeKind kind = DateTimeKind.Utc)
        => new DateTime(date.Year, 12, 1, 0, 0, 0, kind).LastDayOfMonth();

    /// <summary>
    /// Returns the absolute difference in months between two dates.
    /// </summary>
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

    /// <summary>
    /// Returns a human-readable relative date string (e.g. "2 days ago").
    /// </summary>
    public static string ToRelativeDateString(this DateTime date) => GetRelativeDateValue(date, DateTime.UtcNow);

    /// <summary>
    /// Returns a human-readable relative date string compared to <see cref="DateTime.UtcNow"/>.
    /// </summary>
    public static string ToRelativeDateStringUtc(this DateTime date) => GetRelativeDateValue(date, DateTime.UtcNow);

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

    /// <summary>
    /// Generates a sequence of consecutive dates starting from the specified date.
    /// </summary>
    public static IEnumerable<DateTime> ToArray(this DateTime input, int length = 1)
    {
        length = length > 0 ? length : 1;
        return Enumerable.Range(0, length).Select(a => input.AddDays(a));
    }

    /// <summary>
    /// Joins a list of strings into a single string separated by <see cref="Environment.NewLine"/>.
    /// </summary>
    public static string List2String(this List<string> input)
    {
        var sb = new StringBuilder();
        foreach (var s in input) sb.Append(s + Environment.NewLine);
        return sb.ToString();
    }

    /// <summary>
    /// Gets the <see cref="DescriptionAttribute"/> value for the specified enum member, or its string representation.
    /// </summary>
    public static string GetDescription<T>(this T enumerationValue)
    {
        if (enumerationValue is null) throw new ArgumentNullException(nameof(enumerationValue));
        var type = enumerationValue.GetType();
        if (!type.IsEnum)
            throw new ArgumentException("EnumerationValue must be of Enum type", nameof(enumerationValue));
        //Tries to find a DescriptionAttribute for a potential friendly name for the enum
        var memberInfo = type.GetMember(enumerationValue.ToString()!);
        if (memberInfo.IsAny())
        {
            var attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attrs.IsAny())
            {
                //Pull out the description value
                return ((DescriptionAttribute)attrs[0]).Description;
            }
        }
        //If we have no description attribute, just return the ToString of the enum
        return enumerationValue.ToString()!;
    }

    private static Dictionary<string, object> dEnumLookup { get; set; } = [];

    /// <summary>
    /// UNFINISHED, an expansion of ParseEnum, use a static dictionary for speedy lookups?
    /// </summary>
#pragma warning disable IDE0060 // Remove unused parameter
    public static T ParseEnumFAST<T>(this string value, [CallerMemberName] string caller = "")
    {
        //TODO: write unit test for this, if you have two different enums with the same value, it'll return the wrong value...
        //i.e. enum1.MyVal and enum2.MyVal
        if (!dEnumLookup.TryGetValue(value, out object? result))
        {
            var val = (T)Enum.Parse(typeof(T), value, true);
            dEnumLookup.Add(value, val);
            return val;
        }
        return (T)result;
    }
#pragma warning restore IDE0060 // Remove unused parameter

    /// <summary>
    /// Parses a string value into the specified <see cref="Enum"/> type (case-insensitive).
    /// </summary>
    public static T ParseEnum<T>(this string value) where T : struct =>
#if NET9_0_OR_GREATER
        Enum.Parse<T>(value, true);
#else
        (T)Enum.Parse(typeof(T), value, true);
#endif

    /// <summary>
    /// Attempts to parse a string value into the specified <see cref="Enum"/> type.
    /// </summary>
    public static bool TryParseEnum<T>(this string value, out T resultInputType, bool ignoreCase = true)
        where T : struct
    {
        resultInputType = default;
        if (Enum.TryParse(value, ignoreCase, out T result))
        {
            resultInputType = result;
            return true;
        }
        else
            return false;
    }

    /// <summary>
    /// Returns a random value from the dictionary.
    /// </summary>
    public static V GetRandomDValue<T, V>(this Dictionary<T, V> d) where T : notnull
    {
        var keyList = new List<T>(d.Keys);
        var rand = new Random();
        var randomKey = keyList[rand.Next(keyList.Count)];
        return d[randomKey];
    }

    /// <summary>
    /// Determines whether the <see cref="IEnumerable{T}"/> is <see langword="null"/> or empty.
    /// </summary>
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? data) => data is null || !data.Any();

    /// <summary>
    /// Determines whether the <see cref="List{T}"/> is <see langword="null"/> or empty.
    /// </summary>
    public static bool IsNullOrEmpty<T>(this List<T>? data) => data is null || data.Count == 0;

    /// <summary>
    /// Determines whether the array is <see langword="null"/> or empty.
    /// </summary>
    public static bool IsNullOrEmpty<T>(this T[]? data) => data is null || data.Length == 0;

    /// <summary>
    /// Determines whether the <see cref="IEnumerable{T}"/> is not <see langword="null"/> and contains elements.
    /// </summary>
    public static bool IsAny<T>(this IEnumerable<T> data) => data is not null && data.Any();

    /// <summary>
    /// Determines whether the <see cref="List{T}"/> is not <see langword="null"/> and contains elements.
    /// </summary>
    public static bool IsAny<T>(this List<T> data) => data is not null && data.Count > 0;

    /// <summary>
    /// Determines whether the array is not <see langword="null"/> and contains elements.
    /// </summary>
    public static bool IsAny<T>(this T[] data) => data is not null && data.Length > 0;

    /// <summary>
    /// Parses a string to a <see cref="bool"/>, treating "1" as <see langword="true"/>.
    /// </summary>
    public static bool ToBoolean(this string input)
    {
        if (input == "1") input = "true";
        if (bool.TryParse(input.Trim(), out var output))
            return output;
        else
            return false;
    }

    #region ParseDecimal
    /// <summary>
    /// Parses a string to a <see cref="decimal"/>.
    /// </summary>
    public static decimal ToDecimal(this string input) => ParseDecimal(input);

    /// <summary>
    /// Parses a string to a nullable <see cref="decimal"/>.
    /// </summary>
#pragma warning disable IDE0060 // Remove unused parameter
    public static decimal? ToDecimal(this string input, bool nullable) => ParseDecimal(input);
#pragma warning restore IDE0060 // Remove unused parameter

    private static decimal ParseDecimal(string input)
    {
        var output = 0m;
        if (!string.IsNullOrWhiteSpace(input) && !decimal.TryParse(input, out output))
            throw new GenericException("TryParse failed");
        return output;
    }
    #endregion

    #region ParseInt
    //public static int ToInt(this object input) => ParseInt(input);

    /// <summary>
    /// Parses a string to an <see cref="int"/>.
    /// </summary>
    public static int ToInt(this string input) => ParseInt(input);

    //public static int ToInt(this object input, ref int result)
    //{
    //    result = ParseInt(input);
    //    return result;
    //}
    //private static int ParseInt(object input) => ParseInt((input ?? string.Empty).ToString()!, 0);

    private static int ParseInt(string input) => ParseInt(input, 0);
    private static int ParseInt(string input, int _def)
    {
        var output = _def;
        if (string.IsNullOrWhiteSpace(input))
            output = _def;
        else
        {
            try { output = int.Parse(input.Trim()); }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }
        return output;
    }
    #endregion

    #region ParseDateTime
    /// <summary>
    /// Parses an object to a <see cref="DateTime"/>.
    /// </summary>
    public static DateTime ToDateTime(this object input) => ParseDateTime(input);

    /// <summary>
    /// Parses an object to a <see cref="DateTime"/> and returns only the date component.
    /// </summary>
    public static DateTime ToDate(this object input) => ParseDateTime(input).Date;

    private static DateTime ParseDateTime(object input) => ParseDateTime((input ?? string.Empty).ToString()!);

    private static DateTime ParseDateTime(string input) => ParseDateTime(input, DateTime.MinValue);

    private static DateTime ParseDateTime(string input, DateTime _def)
    {
        DateTime output = _def;
        if (string.IsNullOrWhiteSpace(input))
            output = _def;
        else
        {
            try { output = DateTime.Parse(input.Trim()); }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }
        return output;
    }
    #endregion

    /// <summary>
    /// Splits an array into chunks of the specified size.
    /// </summary>
    public static IEnumerable<IEnumerable<T>> Split<T>(this T[] array, int size)
    {
        for (var i = 0; i < (float)array.Length / size; i++)
        {
            yield return array.Skip(i * size).Take(size);
        }
    }

    /// <summary>
    /// GZip using integrated .NET compression library.
    /// </summary>
    public static byte[] Compress(this byte[] data)
    {
        if (data.IsNullOrEmpty()) return data;
        using var compressedStream = new MemoryStream();
        using var zipStream = new GZipStream(compressedStream, CompressionMode.Compress);
        zipStream.Write(data, 0, data.Length);
        zipStream.Close();
        return compressedStream.ToArray();
    }

    /// <summary>
    /// UnGZip using integrated .NET compression library.
    /// </summary>
    public static byte[] Decompress(this byte[] data)
    {
        if (data.IsNullOrEmpty()) return data;
        using var compressedStream = new MemoryStream(data);
        using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        zipStream.CopyTo(resultStream);
        return resultStream.ToArray();
    }

    /// <summary>
    /// Converts a byte count to kilobytes.
    /// </summary>
    public static double GetSizeInKB(this long bytes) => bytes / 1024d;

    /// <summary>
    /// Converts a byte count to megabytes.
    /// </summary>
    public static double GetSizeInMB(this long bytes) => bytes.GetSizeInKB() / 1024;

    /// <summary>
    /// Converts a byte count to gigabytes.
    /// </summary>
    public static double GetSizeInGB(this long bytes) => bytes.GetSizeInMB() / 1024;

    /// <summary>
    /// Returns the last <paramref name="N"/> elements of the sequence.
    /// </summary>
    public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int N) => source.Skip(Math.Max(0, source.Count() - N));
}
