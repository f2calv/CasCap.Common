namespace CasCap.Common.Net.Tests;

using CasCap.Common.Services;
using System.Text;
using System.Text.Json;

/// <summary>
/// Tests for <see cref="HttpClientBase"/> protected HTTP methods.
/// </summary>
public class HttpClientBaseTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    private static TestHttpClient CreateClient(MockHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TestHttpClient();
        client.SetDependencies(httpClient, new LoggerFactory().CreateLogger<TestHttpClient>());
        return client;
    }

    #region PostJson

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJsonAsync_Success_DeserializesResult()
    {
        var expected = new TestPayload { Id = 1, Name = "test" };
        var handler = MockHandler.ForJson(expected);
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", new { Id = 1 });

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("test", result.Name);
        Assert.Null(error);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJsonAsync_Error_DeserializesError()
    {
        var handler = MockHandler.ForError(HttpStatusCode.BadRequest, new ErrorPayload { Message = "bad" });
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", new { Id = 1 });

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Equal("bad", error.Message);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_NullBody_SendsEmptyJsonObject()
    {
        string? capturedBody = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
        }, new TestPayload { Id = 0, Name = "empty" });
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", null);

        Assert.NotNull(result);
        Assert.Equal("{}", capturedBody);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_WithHeaders_SendsHeaders()
    {
        string? headerValue = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            headerValue = req.Headers.GetValues("X-Custom").FirstOrDefault();
            await Task.CompletedTask;
        }, new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        var headers = new List<(string name, string value)> { ("X-Custom", "myvalue") };
        var (result, error) = await client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", new { Id = 1 }, headers: headers);

        Assert.Equal("myvalue", headerValue);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_FullUrl_OverridesBaseAddress()
    {
        Uri? capturedUri = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            capturedUri = req.RequestUri;
            await Task.CompletedTask;
        }, new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await client.TestPostJsonAsync<TestPayload, ErrorPayload>("http://other-host/api/test", new { Id = 1 });

        Assert.NotNull(capturedUri);
        Assert.Equal("other-host", capturedUri.Host);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_ReturnsStatusCodeAndHeaders()
    {
        var handler = MockHandler.ForJsonWithHeaders(
            new TestPayload { Id = 1, Name = "test" },
            ("X-Response-Id", "abc"));
        var client = CreateClient(handler);

        var res = await client.TestPostJson<TestPayload, ErrorPayload>("/api/test", new { Id = 1 });

        Assert.Equal(HttpStatusCode.OK, res.statusCode);
        Assert.Equal("abc", res.responseHeaders.GetValues("X-Response-Id").First());
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_Timeout_ThrowsOperationCanceled()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", new { Id = 1 }, timeout: TimeSpan.FromMilliseconds(50)));
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_ResultAsString_ReturnsRawString()
    {
        var handler = MockHandler.ForRawString("raw response");
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<string, string>("/api/test", new { Id = 1 });

        Assert.Equal("raw response", result);
        Assert.Null(error);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_ResultAsBytes_ReturnsRawBytes()
    {
        var expected = new byte[] { 1, 2, 3, 4 };
        var handler = MockHandler.ForRawBytes(expected);
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<byte[], string>("/api/test", new { Id = 1 });

        Assert.NotNull(result);
        Assert.Equal(expected, result);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_ErrorAsString_ReturnsRawErrorString()
    {
        var handler = MockHandler.ForRawError(HttpStatusCode.InternalServerError, "something broke");
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<string, string>("/api/test", new { Id = 1 });

        Assert.Null(result);
        Assert.Equal("something broke", error);
    }

    #endregion

    #region PostBytes

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostBytesAsync_Success_DeserializesResult()
    {
        var expected = new TestPayload { Id = 2, Name = "bytes" };
        var handler = MockHandler.ForJson(expected);
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01, 0x02]);

        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
        Assert.Null(error);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostBytesAsync_Error_DeserializesError()
    {
        var handler = MockHandler.ForError(HttpStatusCode.BadRequest, new ErrorPayload { Message = "bad upload" });
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01]);

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Equal("bad upload", error.Message);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostBytes_WithHeaders_SendsHeaders()
    {
        string? headerValue = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            headerValue = req.Headers.GetValues("X-Upload-Id").FirstOrDefault();
            await Task.CompletedTask;
        }, new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        var headers = new List<(string name, string value)> { ("X-Upload-Id", "upload-123") };
        var (result, error) = await client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01], headers: headers);

        Assert.Equal("upload-123", headerValue);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostBytes_SetsContentType()
    {
        string? contentType = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            contentType = req.Content!.Headers.ContentType?.MediaType;
            await Task.CompletedTask;
        }, new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01], mediaType: "image/png");

        Assert.Equal("image/png", contentType);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostBytes_Timeout_ThrowsOperationCanceled()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01], timeout: TimeSpan.FromMilliseconds(50)));
    }

    #endregion

    #region Get

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_Success_DeserializesResult()
    {
        var expected = new TestPayload { Id = 3, Name = "get" };
        var handler = MockHandler.ForJson(expected);
        var client = CreateClient(handler);

        var (result, error) = await client.TestGetAsync<TestPayload, ErrorPayload>("/api/data");

        Assert.NotNull(result);
        Assert.Equal(3, result.Id);
        Assert.Equal("get", result.Name);
        Assert.Null(error);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_Error_DeserializesError()
    {
        var handler = MockHandler.ForError(HttpStatusCode.NotFound, new ErrorPayload { Message = "not found" });
        var client = CreateClient(handler);

        var (result, error) = await client.TestGetAsync<TestPayload, ErrorPayload>("/api/data");

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Equal("not found", error.Message);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_WithHeaders_SendsHeaders()
    {
        string? headerValue = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            headerValue = req.Headers.GetValues("Authorization").FirstOrDefault();
            await Task.CompletedTask;
        }, new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        var headers = new List<(string name, string value)> { ("Authorization", "Bearer token123") };
        var (result, error) = await client.TestGetAsync<TestPayload, ErrorPayload>("/api/data", headers: headers);

        Assert.Equal("Bearer token123", headerValue);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task Get_ReturnsStatusCodeAndHeaders()
    {
        var handler = MockHandler.ForJsonWithHeaders(
            new TestPayload { Id = 1, Name = "test" },
            ("X-Total-Count", "42"));
        var client = CreateClient(handler);

        var res = await client.TestGet<TestPayload, ErrorPayload>("/api/data");

        Assert.Equal(HttpStatusCode.OK, res.statusCode);
        Assert.Equal("42", res.responseHeaders.GetValues("X-Total-Count").First());
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_FullUrl_OverridesBaseAddress()
    {
        Uri? capturedUri = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            capturedUri = req.RequestUri;
            await Task.CompletedTask;
        }, new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await client.TestGetAsync<TestPayload, ErrorPayload>("http://external-api.com/data");

        Assert.NotNull(capturedUri);
        Assert.Equal("external-api.com", capturedUri.Host);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task Get_Timeout_ThrowsOperationCanceled()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestGetAsync<TestPayload, ErrorPayload>("/api/data", timeout: TimeSpan.FromMilliseconds(50)));
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_ResultAsString_ReturnsRawString()
    {
        var handler = MockHandler.ForRawString("plain text");
        var client = CreateClient(handler);

        var (result, error) = await client.TestGetAsync<string, string>("/api/data");

        Assert.Equal("plain text", result);
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_ResultAsBytes_ReturnsRawBytes()
    {
        var expected = new byte[] { 10, 20, 30 };
        var handler = MockHandler.ForRawBytes(expected);
        var client = CreateClient(handler);

        var (result, error) = await client.TestGetAsync<byte[], string>("/api/data");

        Assert.NotNull(result);
        Assert.Equal(expected, result);
    }

    #endregion

    #region CancellationToken

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_CancellationToken_Honored()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", cancellationToken: cts.Token));
    }

    [Fact, Trait("Category", "HttpClientBase")]
    public async Task Get_CancellationToken_Honored()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestGetAsync<TestPayload, ErrorPayload>("/api/data", cancellationToken: cts.Token));
    }

    #endregion
}

#region Test helpers

/// <summary>
/// Test response payload.
/// </summary>
public class TestPayload
{
    /// <summary>
    /// Payload identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Payload name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Test error payload.
/// </summary>
public class ErrorPayload
{
    /// <summary>
    /// Error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Concrete subclass of <see cref="HttpClientBase"/> that exposes the protected methods for testing.
/// </summary>
public class TestHttpClient : HttpClientBase
{
    public void SetDependencies(HttpClient httpClient, ILogger logger)
    {
        Client = httpClient;
        _logger = logger;
    }

    public Task<(TResult? result, TError? error)> TestPostJsonAsync<TResult, TError>(
        string requestUri, object? req = null, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null, string mediaType = "application/json",
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => PostJsonAsync<TResult, TError>(requestUri, req, timeout, headers, mediaType, cancellationToken);

    public Task<(TResult? result, TError? error, HttpStatusCode statusCode, HttpResponseHeaders responseHeaders)> TestPostJson<TResult, TError>(
        string requestUri, object? req = null, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null, string mediaType = "application/json",
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => PostJson<TResult, TError>(requestUri, req, timeout, headers, mediaType, cancellationToken);

    public Task<(TResult? result, TError? error)> TestPostBytesAsync<TResult, TError>(
        string requestUri, byte[] bytes, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null, string mediaType = "application/octet-stream",
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => PostBytesAsync<TResult, TError>(requestUri, bytes, timeout, headers, mediaType, cancellationToken);

    public Task<(TResult? result, TError? error)> TestGetAsync<TResult, TError>(
        string requestUri, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null,
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => GetAsync<TResult, TError>(requestUri, timeout, headers, cancellationToken);

    public Task<(TResult? result, TError? error, HttpStatusCode statusCode, HttpResponseHeaders responseHeaders)> TestGet<TResult, TError>(
        string requestUri, TimeSpan? timeout = null,
        List<(string name, string value)>? headers = null,
        CancellationToken cancellationToken = default)
        where TResult : class where TError : class
        => Get<TResult, TError>(requestUri, timeout, headers, cancellationToken);
}

/// <summary>
/// Mock <see cref="HttpMessageHandler"/> for testing HTTP requests without a real server.
/// </summary>
public class MockHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    private MockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request, cancellationToken);

    public static MockHandler ForJson<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new MockHandler((_, _) =>
        {
            var json = JsonSerializer.Serialize(payload);
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });
    }

    public static MockHandler ForJsonWithHeaders<T>(T payload, params (string name, string value)[] headers)
    {
        return new MockHandler((_, _) =>
        {
            var json = JsonSerializer.Serialize(payload);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            foreach (var (name, value) in headers)
                response.Headers.Add(name, value);
            return Task.FromResult(response);
        });
    }

    public static MockHandler ForError<T>(HttpStatusCode statusCode, T errorPayload)
    {
        return new MockHandler((_, _) =>
        {
            var json = JsonSerializer.Serialize(errorPayload);
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });
    }

    public static MockHandler ForRawString(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new MockHandler((_, _) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            };
            return Task.FromResult(response);
        });
    }

    public static MockHandler ForRawError(HttpStatusCode statusCode, string content)
    {
        return new MockHandler((_, _) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            };
            return Task.FromResult(response);
        });
    }

    public static MockHandler ForRawBytes(byte[] bytes, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new MockHandler((_, _) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new ByteArrayContent(bytes)
            };
            return Task.FromResult(response);
        });
    }

    public static MockHandler WithCapture<T>(Func<HttpRequestMessage, Task> capture, T payload)
    {
        return new MockHandler(async (req, _) =>
        {
            await capture(req);
            var json = JsonSerializer.Serialize(payload);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return response;
        });
    }

    public static MockHandler WithDelay<T>(TimeSpan delay, T payload)
    {
        return new MockHandler(async (_, ct) =>
        {
            await Task.Delay(delay, ct);
            var json = JsonSerializer.Serialize(payload);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
    }
}

#endregion
