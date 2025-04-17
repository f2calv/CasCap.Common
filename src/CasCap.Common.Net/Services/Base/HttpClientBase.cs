namespace CasCap.Services;

public abstract class HttpClientBase
{
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    protected ILogger _logger;
    protected HttpClient _client;
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

    protected virtual async Task<(TResult? result, TError? error)> PostJsonAsync<TResult, TError>(string requestUri, object? req = null, TimeSpan? timeout = null, List<(string name, string value)>? headers = null, string mediaType = "application/json", CancellationToken cancellationToken = default)
        where TResult : class
        where TError : class
    {
        var res = await PostJson<TResult, TError>(requestUri, req, timeout, headers, mediaType, cancellationToken);
        return (res.result, res.error);
    }

    protected virtual async Task<(TResult? result, TError? error, HttpStatusCode statusCode, HttpResponseHeaders responseHeaders)>
        PostJson<TResult, TError>(string requestUri, object? req = null, TimeSpan? timeout = null, List<(string name, string value)>? additionalHeaders = null, string mediaType = "application/json", CancellationToken cancellationToken = default)
        where TResult : class
        where TError : class
    {
        (TResult? result, TError? error, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders) tpl;
        var url = requestUri.StartsWith("http") ? requestUri : $"{_client.BaseAddress}{requestUri}";//allows us to override base url
        //_logger.LogDebug("{className} {httpMethod}\t{url}", nameof(HttpClientBase), HttpMethod.Post, url);
        var json = req!.ToJson();
        using (var request = new HttpRequestMessage(HttpMethod.Post, url))//needs full url as a string as System.Uri can't cope with a colon
        {
            request.Content = new StringContent(json, Encoding.UTF8, mediaType);
            request.Headers.AddOrOverwrite(additionalHeaders);
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);//need to create a new .NET Standard extension method to handle GetCT(timeout)
            tpl = await HandleResult<TResult, TError>(response, cancellationToken);
        }
        return tpl;
    }

    protected virtual async Task<(TResult? result, TError? error)>
        PostBytesAsync<TResult, TError>(string requestUri, byte[] bytes, TimeSpan? timeout = null, List<(string name, string value)>? headers = null, string mediaType = "application/octet-stream", CancellationToken cancellationToken = default)
        where TResult : class
        where TError : class
    {
        var res = await PostBytes<TResult, TError>(requestUri, bytes, timeout, headers, mediaType, cancellationToken);
        return (res.result, res.error);
    }

    protected virtual async Task<(TResult? result, TError? error, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders)>
        PostBytes<TResult, TError>(string requestUri, byte[] bytes, TimeSpan? timeout = null, List<(string name, string value)>? additionalHeaders = null, string mediaType = "application/octet-stream", CancellationToken cancellationToken = default)
        where TResult : class
        where TError : class
    {
        (TResult? result, TError? error, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders) tpl;
        var url = requestUri.StartsWith("http") ? requestUri : $"{_client.BaseAddress}{requestUri}";//allows us to override base url
        //_logger.LogDebug("{className} {httpMethod}\t{url}", nameof(HttpClientBase), HttpMethod.Post, url);
        using (var request = new HttpRequestMessage(HttpMethod.Post, url))
        {
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            //request.Headers.Add("Content-Type", mediaType);
            request.Headers.AddOrOverwrite(additionalHeaders);
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            tpl = await HandleResult<TResult, TError>(response, cancellationToken);
        }
        return tpl;
    }

    protected virtual async Task<(TResult? result, TError? error)>
        GetAsync<TResult, TError>(string requestUri, TimeSpan? timeout = null, List<(string name, string value)>? headers = null, CancellationToken cancellationToken = default)
        where TResult : class
        where TError : class
    {
        var res = await Get<TResult, TError>(requestUri, timeout, headers, cancellationToken);
        return (res.result, res.error);
    }

    protected virtual async Task<(TResult? result, TError? error, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders)>
        Get<TResult, TError>(string requestUri, TimeSpan? timeout = null, List<(string name, string value)>? headers = null, CancellationToken cancellationToken = default)
        where TResult : class
        where TError : class
    {
        var url = requestUri.StartsWith("http") ? requestUri : $"{_client.BaseAddress}{requestUri}";//allows us to override base url
        //_logger.LogDebug("{className} {httpMethod}\t{url}", nameof(HttpClientBase), HttpMethod.Post, url);
        //todo: add in headers?
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        return await HandleResult<TResult, TError>(response, cancellationToken);
    }

    private async Task<(TResult? result, TError? error, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders)>
        HandleResult<TResult, TError>(HttpResponseMessage response, CancellationToken cancellationToken)
        where TResult : class
        where TError : class
    {
        (TResult? result, TError? error, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders) tpl;
        tpl.httpStatusCode = response.StatusCode;
        tpl.responseHeaders = response.Headers;
        if (response.IsSuccessStatusCode)
        {
            if (typeof(TResult).Equals(typeof(string)))
                tpl.result = (TResult)(object)await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            else if (typeof(TResult).Equals(typeof(byte[])))
                tpl.result = (TResult)(object)await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            else
                tpl.result = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).FromJson<TResult>();
            tpl.error = default;
        }
        else
        {
            if (typeof(TError).Equals(typeof(string)))
                tpl.error = (TError)(object)await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            else if (typeof(TError).Equals(typeof(byte[])))
                tpl.error = (TError)(object)await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            else
                tpl.error = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).FromJson<TError>();
            _logger.LogError("{className} StatusCode={StatusCode}, RequestUri={RequestUri}", nameof(HttpClientBase), response.StatusCode, response.RequestMessage?.RequestUri);
            //var err = $"requestUri= fail";
            //if (response.RequestMessage.Content.)
            //if (req is not null) err += $"{json}";
            //throw new Exception(err);
            tpl.result = default;
        }
        return tpl;
    }
}
