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
