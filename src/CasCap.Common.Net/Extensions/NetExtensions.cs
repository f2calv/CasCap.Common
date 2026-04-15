namespace CasCap.Common.Extensions;

/// <summary>Extension methods for HTTP headers, query strings and request utilities.</summary>
public static class NetExtensions
{
    /// <summary>
    /// Retrieves the first value for the specified response header name, or <c>null</c> if not found.
    /// </summary>
    public static string? TryGetValue(this HttpResponseHeaders headers, string name)
    {
        if (headers.TryGetValues(name, out var headerValues))
            return headerValues.FirstOrDefault();
        return null;
    }

    /// <summary>Converts a <see cref="NameValueCollection"/> to a URL-encoded query string.</summary>
    public static string ToQueryString(this NameValueCollection nvc)
    {
        var array = (from key in nvc.AllKeys
                     from value in nvc.GetValues(key)!
                     select $"{WebUtility.UrlEncode(key)}={WebUtility.UrlEncode(value)}")
                    .ToArray();
        return "?" + string.Join("&", array);
    }

    /// <summary>
    /// Adds all members of the name/value collection to the headers, if header name already exists the value is overwritten.
    /// </summary>
    public static void AddOrOverwrite(this HttpRequestHeaders headers, List<(string name, string value)>? additionalHeaders)
    {
        if (!additionalHeaders.IsNullOrEmpty())
            foreach (var header in additionalHeaders!)
                headers.AddOrOverwrite(header.name, header.value);
    }

    /// <inheritdoc cref="AddOrOverwrite(HttpRequestHeaders, List{ValueTuple{string, string}}?)"/>
    public static void AddOrOverwrite(this HttpRequestHeaders headers, Dictionary<string, string>? additionalHeaders)
    {
        if (!additionalHeaders.IsNullOrEmpty())
            foreach (var header in additionalHeaders!)
                headers.AddOrOverwrite(header.Key, header.Value);
    }

    /// <summary>Adds a name/value to the headers, if header name already exists the value is overwritten.</summary>
    public static void AddOrOverwrite(this HttpRequestHeaders headers, string name, string value)
    {
        if (headers.TryGetValues(name, out var _))
            headers.Remove(name);
        headers.Add(name, value);
    }

    /// <summary>
    /// Sets the <c>Authorization</c> header to HTTP Basic credentials derived from the
    /// supplied <paramref name="username"/> and <paramref name="password"/>.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> to configure.</param>
    /// <param name="username">The Basic authentication username.</param>
    /// <param name="password">The Basic authentication password.</param>
    public static void SetBasicAuth(this HttpClient client, string username, string password)
    {
        var base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64);
    }

    /// <summary>
    /// Returns the full HTTP Basic <c>Authorization</c> header value (e.g. <c>"Basic dXNlcjpwYXNz"</c>)
    /// suitable for use with SignalR hub connections or other HTTP clients.
    /// </summary>
    /// <param name="username">The Basic authentication username.</param>
    /// <param name="password">The Basic authentication password.</param>
    public static string GetBasicAuthHeaderValue(string username, string password)
    {
        var base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        return $"Basic {base64}";
    }

    //public static async Task<T?> ReadAsJsonAsync<T>(this HttpContent content)//for .NET Standard compatibility
    //{
    //    var json = await content.ReadAsStringAsync().ConfigureAwait(false);
    //    T? value = json.FromJson<T>();
    //    return value;
    //}

    //public static async Task<T> ReadAsJsonAsyncS<T>(this HttpContent content)//for .NET Standard compatibility
    //{
    //    var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
    //    T value = stream.FromJSON<T>();
    //    return value;
    //}

    //public static Task<HttpResponseMessage> PostAsJsonAsync<T>(this HttpClient httpClient, string url, T obj)//for .NET Standard compatibility
    //{
    //    var json = obj!.ToJson();
    //    var content = new StringContent(json, Encoding.UTF8, "application/json");
    //    return httpClient.PostAsync(url, content);
    //}

    //public static Task<HttpResponseMessage> PutAsJsonAsync<T>(this HttpClient httpClient, string url, T obj)//for .NET Standard compatibility
    //{
    //    return httpClient.PostAsJsonAsync<T>(url, obj);
    //}
}
