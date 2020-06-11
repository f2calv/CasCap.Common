using CasCap.Common.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace CasCap.Services
{
    public abstract class HttpClientBase
    {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        protected ILogger _logger;
        protected HttpClient _client;
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        protected virtual async Task<T?> PostJsonAsync<T>(string requestUri, object? req = null, TimeSpan? timeout = null, List<(string name, string value)>? headers = null, string mediaType = "application/json") where T : class
            => (await PostJson<T>(requestUri, req, timeout, headers, mediaType)).obj;

        protected virtual async Task<(T? obj, HttpStatusCode statusCode, HttpResponseHeaders responseHeaders)> PostJson<T>(string requestUri, object? req = null, TimeSpan? timeout = null, List<(string name, string value)>? headers = null, string mediaType = "application/json") where T : class
        {
            (T? obj, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders) tpl;
            var url = requestUri.StartsWith("http") ? requestUri : $"{_client.BaseAddress}{requestUri}";//allows us to override base url
            //_logger.LogDebug($"{HttpMethod.Post}\t{url}");
            var json = req!.ToJSON();
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))//needs full url as a string as System.Uri can't cope with a colon
            {
                request.Content = new StringContent(json, Encoding.UTF8, mediaType);
                AddRequestHeaders(request, headers);
                using (var response = await _client.SendAsync(request).ConfigureAwait(false))//need to create a new .NET Standard extension method to handle GetCT(timeout)
                    tpl = await HandleResult<T>(response);
            }
            return tpl;
        }

        protected virtual async Task<T?> PostBytesAsync<T>(string requestUri, byte[] bytes, TimeSpan? timeout = null, List<(string name, string value)>? headers = null, string mediaType = "application/octet-stream") where T : class
            => (await PostBytes<T>(requestUri, bytes, timeout, headers, mediaType)).obj;

        protected virtual async Task<(T? obj, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders)> PostBytes<T>(string requestUri, byte[] bytes, TimeSpan? timeout = null, List<(string name, string value)>? headers = null, string mediaType = "application/octet-stream") where T : class
        {
            (T? obj, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders) tpl;
            var url = requestUri.StartsWith("http") ? requestUri : $"{_client.BaseAddress}{requestUri}";//allows us to override base url
            //_logger.LogDebug($"{HttpMethod.Post}\t{url}");
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new ByteArrayContent(bytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
                //request.Headers.Add("Content-Type", mediaType);
                AddRequestHeaders(request, headers);
                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                    tpl = await HandleResult<T>(response);
            }
            return tpl;
        }

        protected virtual async Task<T?> GetAsync<T>(string requestUri, TimeSpan? timeout = null, List<(string name, string value)>? headers = null) where T : class
            => (await Get<T>(requestUri, timeout, headers)).obj;

        protected virtual async Task<(T? obj, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders)> Get<T>(string requestUri, TimeSpan? timeout = null, List<(string name, string value)>? headers = null) where T : class
        {
            (T? obj, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders) tpl;
            var url = requestUri.StartsWith("http") ? requestUri : $"{_client.BaseAddress}{requestUri}";//allows us to override base url
            //_logger.LogDebug($"{HttpMethod.Get}\t{url}");
            //todo: add in headers?
            using (var response = await _client.GetAsync(url, HttpCompletionOption.ResponseContentRead, GetCT(timeout)).ConfigureAwait(false))
                tpl = await HandleResult<T>(response);
            return tpl;
        }

        async Task<(T? obj, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders)> HandleResult<T>(HttpResponseMessage response) where T : class
        {
            (T? obj, HttpStatusCode httpStatusCode, HttpResponseHeaders responseHeaders) tpl;
            tpl.httpStatusCode = response.StatusCode;
            if (tpl.httpStatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException($"Look likes you need to authenticate first!");
            else if (tpl.httpStatusCode == HttpStatusCode.NotFound)
                throw new UnauthorizedAccessException($"The URL you requested returned a 404, check '{response.RequestMessage.RequestUri}' is correct!");
            tpl.responseHeaders = response.Headers;
            if (response.IsSuccessStatusCode)
            {
                if (typeof(T).Equals(typeof(string)))
                    tpl.obj = (T)(object)await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                else if (typeof(T).Equals(typeof(byte[])))
                    tpl.obj = (T)(object)await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                else
                    tpl.obj = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).FromJSON<T>();
            }
            else
            {
                _logger.LogError($"{response.StatusCode}\t{response.RequestMessage.RequestUri}");
                //var err = $"requestUri= fail";
                //if (response.RequestMessage.Content.)
                //if (req != null) err += $"{json}";
                //throw new Exception(err);
                tpl.obj = default(T);
            }
            return tpl;
        }

        void AddRequestHeaders(HttpRequestMessage request, List<(string name, string value)>? headers)
        {
            if (!headers.IsNullOrEmpty())
                foreach (var header in headers!)
                    request.Headers.Add(header.name, header.value);
        }

        //https://stackoverflow.com/questions/46874693/re-using-httpclient-but-with-a-different-timeout-setting-per-request
        CancellationToken GetCT(TimeSpan? timeout = null)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout.HasValue ? timeout.Value : TimeSpan.FromSeconds(90));
            return cts.Token;
        }
    }
}