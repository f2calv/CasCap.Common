using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
namespace CasCap.Common.Extensions
{
    public static class ExtensionHelpers
    {
        #region IsNullOrEmpty & IsNullOrWhiteSpace cannot interpret nullable reference types correctly, needs more research
        //https://github.com/dotnet/roslyn/issues/37995
        //https://github.com/JamesNK/Newtonsoft.Json/pull/2163/commits/fba64bcf9b8f41500da1c1dd75825f3db99cd3b4
        public static bool IsNullOrWhiteSpace(this string? value)
        {
            return value is null || value.Trim() == string.Empty;
            //return string.IsNullOrWhiteSpace(value);
        }

        //public static bool IsNullOrEmpty([NotNullWhen(false)] string? value)
        //{
        //    return string.IsNullOrEmpty(value);
        //}

        //public static bool IsNullOrEmpty(string? value)//conflits with collections extension IsNullOrEmpty
        //{
        //    return string.IsNullOrEmpty(value);
        //    //return input?.Length > 0;
        //}
        #endregion

        public static Dictionary<int, List<T>> GetBatches<T>(this List<T> objects, int batchSize)
        {
            var batches = new Dictionary<int, List<T>>();
            for (var i = 0; i < objects.Count; i++)
            {
                var batchNumber = i / batchSize;
                if (!batches.ContainsKey(batchNumber))
                    batches.Add(batchNumber, new List<T>());
                batches[batchNumber].Add(objects[i]);
            }
            return batches;
        }

        public static ConcurrentDictionary<T, V> ToConcurrentDictionary<T, V>(this Dictionary<T, V> d2) where T : notnull
        {
            var d1 = new ConcurrentDictionary<T, V>();
            foreach (var z in d2)
            {
                if (!d1.TryAdd(z.Key, z.Value))
                    throw new Exception($"AddRange failed due to conflicting key");
            }
            return d1;
        }

        public static ConcurrentDictionary<T, V> AddRange<T, V>(this ConcurrentDictionary<T, V> d1, Dictionary<T, V> d2) where T : notnull
        {
            foreach (var z in d2)
            {
                if (!d1.TryAdd(z.Key, z.Value))
                    throw new Exception($"AddRange failed due to conflicting key");
            }
            return d1;
        }

        public static Dictionary<T, V> AddRange<T, V>(this Dictionary<T, V> d1, Dictionary<T, V> d2) where T : notnull
        {
            foreach (var z in d2)
                d1.Add(z.Key, z.Value);
            return d1;
        }

        public static HashSet<T> AddRange<T>(this HashSet<T> hs, List<T> l)
        {
            foreach (var z in l)
                hs.Add(z);
            return hs;
        }

        public static HashSet<T> AddRange<T>(this HashSet<T> hs, IEnumerable<T> l)
        {
            foreach (var z in l)
                hs.Add(z);
            return hs;
        }

        public static HashSet<T> AddRange<T>(this HashSet<T> hs, HashSet<T> l)
        {
            foreach (var z in l)
                hs.Add(z);
            return hs;
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> l)//can remove if we use .net standard 2.1?
        {
            var hs = new HashSet<T>();
            foreach (var z in l)
                hs.Add(z);
            return hs;
        }

        public static List<DateTime> GetMissingDates(this DateTime dtStart, DateTime dtEnd)
        {
            //todo: plug in known holidays dates somehow?
            var dayGap = dtEnd.Date.Subtract(dtStart).Days;
            var missingDates = Enumerable.Range(1, dayGap).Select(p => dtStart.AddDays(p)).ToArray();
            return missingDates.ToList();
        }

        /// <summary>
        /// Checks if a struct has been instantiated.
        /// </summary>
        public static bool IsNull<T>(this T source) where T : struct => source.Equals(default(T));

        #region datetime
        public static int SecondsTillMidnight(this DateTime dt)
        {
            return dt.SecondsTillMidnight(DateTime.UtcNow);
        }
        public static int SecondsTillMidnight(this DateTime dt, DateTime now)
        {
            var ts = DateTime.UtcNow.Date.AddDays(1) - now;
            return (int)ts.TotalSeconds;//does this round-up?
        }

        public static string GetTimeDifference(this DateTime dtiStart, out DateTime utcNow, bool includeMilliseconds = false)
        {
            utcNow = DateTime.UtcNow;
            return GetTimeDifference(dtiStart, utcNow, includeMilliseconds: includeMilliseconds);
        }

        public static string GetTimeDifference(this DateTime dtiStart, DateTime dtiEnd,
            bool includeSeconds = true, bool includeMinutes = true, bool includeHours = true, bool includeDays = true, bool includeMilliseconds = false)
        {
            var ts = dtiStart > dtiEnd ? dtiStart.Subtract(dtiEnd) : dtiEnd.Subtract(dtiStart);
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
        public static string To_yyyy_MM_dd(this DateTime thisDateTime)
        {
            return thisDateTime.ToString("yyyy-MM-dd");
        }

        static readonly DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime FromUnixTime(this long seconds)
        {
            return epoch.AddSeconds(seconds);
        }

        public static DateTime FromUnixTimeMS(this long milliseconds)
        {
            return epoch.AddMilliseconds(milliseconds);
        }

        public static long ToUnixTime(this DateTime dt)
        {
            return ((DateTimeOffset)dt).ToUnixTimeSeconds();
        }

        public static long ToUnixTimeMS(this DateTime dt)
        {
            return dt.ToUnixTime() * 1000;
        }

        public static bool IsWeekend(this DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        public static bool IsWeekday(this DateTime date)
        {
            return !date.IsWeekend();
        }

        public static DateTime ToUTC(this DateTime dt)
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

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

        public static DateTime FirstDayOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            var diff = dt.DayOfWeek - startOfWeek;
            if (diff < 0) diff += 7;
            return dt.AddDays(-1 * diff).Date;
        }

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
        public static DateTime FirstDayOfMonth(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1);
        }
        public static DateTime LastDayOfMonth(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
        }
        public static DateTime LastDayOfYear(this DateTime date)
        {
            return new DateTime(date.Year, 12, 1).LastDayOfMonth();
        }
        public static int MonthDifference(this DateTime lValue, DateTime rValue)
        {
            return Math.Abs(lValue.Month - rValue.Month + 12 * (lValue.Year - rValue.Year));
        }
        public static string ToString(this DateTime? date)
        {
            return date.ToString(DateTimeFormatInfo.CurrentInfo);
        }
        public static string ToString(this DateTime? date, string format)
        {
            return date.ToString(format, DateTimeFormatInfo.CurrentInfo);
        }
        public static string ToString(this DateTime? date, IFormatProvider provider)
        {
            return date.ToString(provider);
        }
        public static string ToString(this DateTime? date, string format, IFormatProvider provider)
        {
            if (date.HasValue)
                return date.Value.ToString(format, provider);
            else
                return string.Empty;
        }
        public static string ToRelativeDateString(this DateTime date)
        {
            return GetRelativeDateValue(date, DateTime.UtcNow);
        }
        public static string ToRelativeDateStringUtc(this DateTime date)
        {
            return GetRelativeDateValue(date, DateTime.UtcNow);
        }
        static string GetRelativeDateValue(DateTime date, DateTime comparedTo)
        {
            TimeSpan diff = comparedTo.Subtract(date);
            if (diff.TotalDays >= 365)
                return string.Concat("on ", date.ToString("MMMM d, yyyy"));
            if (diff.TotalDays >= 7)
                return string.Concat("on ", date.ToString("MMMM d"));
            else if (diff.TotalDays > 1)
                return string.Format("{0:N0} days ago", diff.TotalDays);
            else if (diff.TotalDays == 1)
                return "yesterday";
            else if (diff.TotalHours >= 2)
                return string.Format("{0:N0} hours ago", diff.TotalHours);
            else if (diff.TotalMinutes >= 60)
                return "more than an hour ago";
            else if (diff.TotalMinutes >= 5)
                return string.Format("{0:N0} minutes ago", diff.TotalMinutes);
            if (diff.TotalMinutes >= 1)
                return "a few minutes ago";
            else
                return "less than a minute ago";
        }

        public static IEnumerable<DateTime> ToArray(this DateTime input, int length = 1)
        {
            length = length > 0 ? length : 1;
            return Enumerable.Range(0, length).Select(a => input.AddDays(a));
        }
        #endregion

        public static string UrlCombine(this string baseUrl, string relativeUrl)
        {
            baseUrl = baseUrl.TrimEnd(new char[] { '/' });
            if (relativeUrl is null)
                relativeUrl = string.Empty;
            relativeUrl = relativeUrl.TrimStart(new char[] { '~' });
            relativeUrl = relativeUrl.TrimStart(new char[] { '/' });
            return baseUrl + "/" + relativeUrl;
        }

        public static string List2String(this List<string> input)
        {
            var sb = new StringBuilder();
            foreach (var s in input) sb.Append(s + Environment.NewLine);
            return sb.ToString();
        }

        public static IEnumerable<string> String2List(this string input)
        {
            foreach (var s in input.Split(new[] { '\r', '\n' }))
                if (!string.IsNullOrWhiteSpace(s))
                    yield return s;
        }

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

        static Dictionary<string, object> dEnumLookup { get; set; } = new();

        /// <summary>
        /// UNFINISHED, an expansion of ParseEnum, use a static dictionary for speedy lookups?
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T ParseEnumFAST<T>(this string value, [CallerMemberName] string caller = "")
        {
            //todo: write unit test for this, if you have two different enums with the same value, it'll return the wrong value...
            //i.e. enum1.MyVal and enum2.MyVal
            if (!dEnumLookup.TryGetValue(value, out object? result))
            {
                var val = (T)Enum.Parse(typeof(T), value, true);
                dEnumLookup.Add(value, val);
                return val;
            }
            return (T)result;
        }

        public static T ParseEnum<T>(this string value) where T : struct => (T)Enum.Parse(typeof(T), value, true);

        public static T? TryParseEnum<T>(this string value, bool ignoreCase = true, [CallerMemberName] string caller = "")
            where T : struct
        {
            T resultInputType;
            if (Enum.TryParse<T>(value, ignoreCase, out resultInputType))
                return resultInputType;
            return null;
        }

        public static V GetRandomDValue<T, V>(this Dictionary<T, V> d) where T : notnull
        {
            var keyList = new List<T>(d.Keys);
            var rand = new Random();
            var randomKey = keyList[rand.Next(keyList.Count)];
            return d[randomKey];
        }

        //check if ienumerable is null or empty
        public static bool IsNullOrEmpty<T>(this IEnumerable<T>? data) => data is null || !data.Any();
        public static bool IsNullOrEmpty<T>(this List<T>? data) => data is null || data.Count == 0;
        public static bool IsNullOrEmpty<T>(this T[]? data) => data is null || data.Length == 0;

        public static bool IsAny<T>(this IEnumerable<T> data) => data is not null && data.Any();
        public static bool IsAny<T>(this List<T> data) => data is not null && data.Count > 0;
        public static bool IsAny<T>(this T[] data) => data is not null && data.Length > 0;

        public static bool ToBoolean(this string input)
        {
            if (input == "1") input = "true";
            if (bool.TryParse(input.Trim(), out var output))
                return output;
            else
                return false;
        }

        #region ParseDecimal
        public static decimal ToDecimal(this string input) => ParseDecimal(input);

        public static decimal? ToDecimal(this string input, bool nullable) => ParseDecimal(input);

        static decimal ParseDecimal(string input)
        {
            var output = 0m;
            if (!string.IsNullOrWhiteSpace(input) && !decimal.TryParse(input, out output))
                throw new Exception("TryParse failed");
            return output;
        }
        #endregion

        #region ParseInt
        //public static int ToInt(this object input) => ParseInt(input);

        public static int ToInt(this string input) => ParseInt(input);

        //public static int ToInt(this object input, ref int result)
        //{
        //    result = ParseInt(input);
        //    return result;
        //}
        static int ParseInt(object input) => ParseInt((input ?? string.Empty).ToString()!, 0);

        static int ParseInt(string input) => ParseInt(input, 0);
        static int ParseInt(string input, int _def)
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
        public static DateTime ToDateTime(this object input) => ParseDateTime(input);

        public static DateTime ToDate(this object input) => ParseDateTime(input).Date;

        static DateTime ParseDateTime(object input) => ParseDateTime((input ?? string.Empty).ToString()!);

        static DateTime ParseDateTime(string input) => ParseDateTime(input, DateTime.MinValue);

        static DateTime ParseDateTime(string input, DateTime _def)
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

        public static IEnumerable<IEnumerable<T>> Split<T>(this T[] array, int size)
        {
            for (var i = 0; i < (float)array.Length / size; i++)
            {
                yield return array.Skip(i * size).Take(size);
            }
        }

        public static string SubstringSafe(this string thisString, int maxLength, bool includeTrailingDots = false)
        {
            if (thisString is not null && maxLength > 0)
            {
                var original = thisString;
                if (includeTrailingDots && maxLength > 3)
                    maxLength += -3;
                if (maxLength < thisString.Length)
                    thisString = thisString.Substring(0, maxLength);
                thisString = thisString.Trim();
                if (thisString.Length > 0 && thisString[thisString.Length - 1] == ',')
                    thisString = thisString.Substring(0, thisString.Length - 1);
                if (includeTrailingDots && original.ToString().Length > maxLength)
                    thisString += "...";
                return thisString;
            }
            return string.Empty;
        }

        #region IO extensions
        public static string Extend(this string root, string folderOrFile)
        {
            var path = Path.Combine(root, folderOrFile);
            return path;
        }

        //public static string ExtendAndCreateDirectory(this string root, string folderOrFile)
        //{
        //    var directory = root.ExtendPath(Path.GetFullPath(folderOrFile));
        //    if (!Directory.Exists(directory))
        //        Directory.CreateDirectory(directory);
        //    return directory;
        //}

        public static void WriteAllBytes(this string path, byte[] bytes)
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is object)
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, bytes);
            }
            else
                throw new Exception($"GetDirectoryName not possible for path '{path}'");
        }

        public static void WriteAllText(this string path, string str)
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is object)
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, str);
            }
            else
                throw new Exception($"GetDirectoryName not possible for path '{path}'");
        }

        public static void AppendTextFile(this string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is object)
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (!File.Exists(path))
                {
                    using var sw = File.CreateText(path);
                    sw.WriteLine(content);
                }
                else
                {
                    try
                    {
                        using var sw = File.AppendText(path);
                        sw.WriteLine(content);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }
                }
            }
            else
                throw new Exception($"GetDirectoryName not possible for path '{path}'");
        }

        public static List<string> ReadTextFile(this string path)
        {
            var output = new List<string>(50000);
            if (File.Exists(path))
            {
                //var count = TotalLines(path);
                //output = new List<string>(count);
                foreach (var line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    output.Add(line);
                }
                //using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                //{
                //    using (var stream = new StreamReader(fs))
                //    {
                //        while (true)
                //        {
                //            var line = stream.ReadLine();
                //            output.Add(line);
                //            if (line is null)
                //                break;
                //        }
                //    }
                //}
            }
            return output;
        }
        #endregion

        public static string Clean(this string thisString, string replacement = "")
        {
            return rgx.Replace(thisString, replacement);
        }

        static Regex rgx { get { return new Regex(cleanPattern, RegexOptions.Compiled); } }

        const string cleanPattern = @"\t|\n|\r";

        public static bool IsEmail(this string thisString)
        {
            //same as new aspNetEmail.EmailMessage().ValidateRegEx
            return thisString is not null && rgxEmail.IsMatch(thisString);
        }

        static Regex rgxEmail { get { return new Regex(emailPattern, RegexOptions.Compiled); } }

        const string emailPattern = @"^((\w+)|(\w+[!#$%&'*+\-,./=?^_`{|}~\w]*[!#$%&'*+\-,/=?^_`{|}~\w]))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,10}|[0-9]{1,3})(\]?)$";

        /// <summary>
        /// GZip using integrated .NET compression library.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
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
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] Decompress(this byte[] data)
        {
            if (data.IsNullOrEmpty()) return data;
            using var compressedStream = new MemoryStream(data);
            using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            zipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }

        public static string ToBase64(this string thisString)
        {
            var bytes = Encoding.UTF8.GetBytes(thisString);
            return Convert.ToBase64String(bytes);
        }

        public static double GetSizeInKB(this long bytes) => bytes / 1024d;

        public static double GetSizeInMB(this long bytes) => bytes.GetSizeInKB() / 1024;

        public static double GetSizeInGB(this long bytes) => bytes.GetSizeInMB() / 1024;

        /// <summary>
        /// Split a string by ';' characters. Accepts nulls :)
        /// </summary>
        public static string[] split(this string _s, char sep = ';')
        {
            return (_s ?? string.Empty).Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int N) => source.Skip(Math.Max(0, source.Count() - N));

        static readonly Regex rgxSanitize = new("[\\~#%&*{}/:<>?|\"-]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex rgxMultipleSpaces = new(@"\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        const string singleSpace = " ";

        /// <summary>
        /// Strips characters that are non-conducive to being in a file name.
        /// </summary>
        public static string? Sanitize(this string? input, string replacement = singleSpace)
        {
            if (input is null) return input;
            var sanitized = rgxSanitize.Replace(input, replacement);
            return replacement == singleSpace ? rgxMultipleSpaces.Replace(sanitized, replacement) : sanitized;
        }
    }
}