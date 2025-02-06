namespace CasCap.Common.Extensions;

public static class NetHelpers
{
    public static string? TryGetValue(this HttpResponseHeaders headers, string name)
    {
        var headerValues = headers.GetValues(name);
        return headerValues.FirstOrDefault();
    }

    public static string ToQueryString(this NameValueCollection nvc)
    {
        var array = (from key in nvc.AllKeys
                     from value in nvc.GetValues(key)!
                     select $"{WebUtility.UrlEncode(key)}={WebUtility.UrlEncode(value)}")
                    .ToArray();
        return "?" + string.Join("&", array);
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
