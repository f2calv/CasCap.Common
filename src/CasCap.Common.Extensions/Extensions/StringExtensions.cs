using System.Text;
using System.Text.RegularExpressions;

namespace CasCap.Common.Extensions;

/// <summary>Extension methods for <see cref="string"/>.</summary>
public static class StringExtensions
{
    /// <summary>Converts a PascalCase name to snake_case (e.g. <c>GetAllState</c> → <c>get_all_state</c>).</summary>
    /// <param name="name">The PascalCase input string.</param>
    /// <returns>The snake_case equivalent.</returns>
    public static string ToSnakeCase(this string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Combines a base URL with a relative URL, handling leading/trailing slashes.</summary>
    public static string UrlCombine(this string baseUrl, string relativeUrl)
    {
        baseUrl = baseUrl.TrimEnd('/');
        relativeUrl ??= string.Empty;
        relativeUrl = relativeUrl.TrimStart('~');
        relativeUrl = relativeUrl.TrimStart('/');
        return baseUrl + "/" + relativeUrl;
    }

    /// <summary>Splits a string by line-break characters, yielding only non-empty lines.</summary>
    public static IEnumerable<string> String2List(this string input)
    {
        foreach (var s in input.Split(['\r', '\n']))
            if (!string.IsNullOrWhiteSpace(s))
                yield return s;
    }

    /// <summary>Returns a substring capped at the specified length, optionally appending trailing dots.</summary>
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

    /// <summary>Removes tab, newline and carriage-return characters from the string.</summary>
    public static string Clean(this string thisString, string replacement = "")
    {
        return rgxClean.Replace(thisString, replacement);
    }

    /// <summary>Determines whether the string is a valid email address.</summary>
    public static bool IsEmail(this string thisString)
    {
        //same as new aspNetEmail.EmailMessage().ValidateRegEx
        return thisString is not null && rgxEmail.IsMatch(thisString);
    }

    /// <summary>Converts a UTF-8 string to its Base64 representation.</summary>
    public static string ToBase64(this string thisString)
    {
        var bytes = Encoding.UTF8.GetBytes(thisString);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Split a string by ';' characters. Accepts nulls :)</summary>
    public static string[] Split(this string _s, char sep = ';')
    {
        return (_s ?? string.Empty).Split([sep], StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Strips characters that are non-conducive to being in a file name.</summary>
    public static string? Sanitize(this string? input, string replacement = SingleSpace)
    {
        if (input is null) return input;
        var sanitized = rgxSanitize.Replace(input, replacement);
        return replacement == SingleSpace ? rgxMultipleSpaces.Replace(sanitized, replacement) : sanitized;
    }

    #region private regex fields

    private static readonly TimeSpan s_regexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex rgxClean = new(@"\t|\n|\r", RegexOptions.Compiled, s_regexTimeout);

    private static readonly Regex rgxEmail = new(
        @"^((\w+)|(\w+[!#$%&'*+\-,./=?^_`{|}~\w]*[!#$%&'*+\-,/=?^_`{|}~\w]))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,10}|[0-9]{1,3})(\]?)$",
        RegexOptions.Compiled, s_regexTimeout);

    private static readonly Regex rgxSanitize = new("[\\~#%&*{}/:<>?|\"-]", RegexOptions.IgnoreCase | RegexOptions.Compiled, s_regexTimeout);

    private static readonly Regex rgxMultipleSpaces = new(@"\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled, s_regexTimeout);

    private const string SingleSpace = " ";

    #endregion

    /// <summary>Masks the middle digits of a phone number, preserving the country code prefix and last two digits.</summary>
    /// <remarks>E.g. <c>+447801438982</c> → <c>+44******82</c>.</remarks>
    public static string MaskPhoneNumber(this string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length < 6)
            return phoneNumber;

        var digits = phoneNumber.AsSpan();
        var prefixLen = digits[0] == '+' ? 3 : 2;
        const int suffixLen = 2;
        var maskLen = digits.Length - prefixLen - suffixLen;
        if (maskLen <= 0)
            return phoneNumber;

        return string.Concat(digits[..prefixLen], new string('*', maskLen), digits[^suffixLen..]);
    }

    /// <summary>Masks the domain of a URI, preserving the scheme and subdomain.</summary>
    /// <remarks>E.g. <c>https://llama-cpp.as34013.net/</c> → <c>https://llama-cpp.***</c>.</remarks>
    public static string MaskEndpoint(this Uri? endpoint)
    {
        if (endpoint is null)
            return string.Empty;

        var host = endpoint.Host;
        var dotIndex = host.IndexOf('.');
        if (dotIndex < 0)
            return $"{endpoint.Scheme}://***";

        var subdomain = host[..dotIndex];
        return $"{endpoint.Scheme}://{subdomain}.***";
    }
}
