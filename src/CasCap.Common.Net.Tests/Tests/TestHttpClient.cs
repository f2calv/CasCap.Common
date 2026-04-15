namespace CasCap.Common.Net.Tests;

using CasCap.Common.Services;

/// <summary>Concrete subclass of <see cref="HttpClientBase"/> that exposes the protected methods for testing.</summary>
public class TestHttpClient : HttpClientBase
{
    /// <summary>Sets the <see cref="HttpClient"/> and <see cref="ILogger"/> dependencies for testing.</summary>
    public void SetDependencies(HttpClient httpClient, ILogger logger)
    {
        Client = httpClient;
        _logger = logger;
    }

    /// <summary>Exposes <see cref="HttpClientBase.PostJsonAsync{TResult, TError}"/> for testing.</summary>
    public Task<(TResult? result, TError? error)> TestPostJsonAsync<TResult, TError>(
        string requestUri, object? req = null, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null, string mediaType = "application/json",
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => PostJsonAsync<TResult, TError>(requestUri, req, timeout, headers, mediaType, cancellationToken);

    /// <summary>Exposes <see cref="HttpClientBase.PostJson{TResult, TError}"/> for testing.</summary>
    public Task<(TResult? result, TError? error, HttpStatusCode statusCode, HttpResponseHeaders responseHeaders)> TestPostJson<TResult, TError>(
        string requestUri, object? req = null, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null, string mediaType = "application/json",
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => PostJson<TResult, TError>(requestUri, req, timeout, headers, mediaType, cancellationToken);

    /// <summary>Exposes <see cref="HttpClientBase.PostBytesAsync{TResult, TError}"/> for testing.</summary>
    public Task<(TResult? result, TError? error)> TestPostBytesAsync<TResult, TError>(
        string requestUri, byte[] bytes, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null, string mediaType = "application/octet-stream",
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => PostBytesAsync<TResult, TError>(requestUri, bytes, timeout, headers, mediaType, cancellationToken);

    /// <summary>Exposes <see cref="HttpClientBase.GetAsync{TResult, TError}"/> for testing.</summary>
    public Task<(TResult? result, TError? error)> TestGetAsync<TResult, TError>(
        string requestUri, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null,
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => GetAsync<TResult, TError>(requestUri, timeout, headers, cancellationToken);

    /// <summary>Exposes <see cref="HttpClientBase.Get{TResult, TError}"/> for testing.</summary>
    public Task<(TResult? result, TError? error, HttpStatusCode statusCode, HttpResponseHeaders responseHeaders)> TestGet<TResult, TError>(
        string requestUri, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null,
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => Get<TResult, TError>(requestUri, timeout, headers, cancellationToken);
}
