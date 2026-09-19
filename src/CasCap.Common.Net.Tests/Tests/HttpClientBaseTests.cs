namespace CasCap.Common.Net.Tests;

using CasCap.Common.Services;
using System.Text;
using System.Text.Json;

/// <summary>Tests for <see cref="HttpClientBase"/> protected HTTP methods.</summary>
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

    /// <summary>Verifies that PostJsonAsync deserializes a successful JSON response into the result type.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJsonAsync_Success_DeserializesResult()
    {
        var expected = new TestPayload { Id = 1, Name = "test" };
        var handler = MockHandler.ForJson(expected);
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", new { Id = 1 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("test", result.Name);
        Assert.Null(error);
    }

    /// <summary>Verifies that PostJsonAsync deserializes an error JSON response into the error type.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJsonAsync_Error_DeserializesError()
    {
        var handler = MockHandler.ForError(HttpStatusCode.BadRequest, new ErrorPayload { Message = "bad" });
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", new { Id = 1 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Equal("bad", error.Message);
    }

    /// <summary>Verifies that PostJson sends an empty JSON object when the body is null.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_NullBody_SendsEmptyJsonObject()
    {
        string? capturedBody = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
        }, new TestPayload { Id = 0, Name = "empty" });
        var client = CreateClient(handler);

        var (result, _) = await client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("{}", capturedBody);
    }

    /// <summary>Verifies that PostJson forwards additional request headers.</summary>
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
        _ = await client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", new { Id = 1 }, headers: headers, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("myvalue", headerValue);
    }

    /// <summary>Verifies that a full URL in PostJson overrides the client base address.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_FullUrl_OverridesBaseAddress()
    {
        Uri? capturedUri = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            capturedUri = req.RequestUri;
        }, new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await client.TestPostJsonAsync<TestPayload, ErrorPayload>("http://other-host/api/test", new { Id = 1 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(capturedUri);
        Assert.Equal("other-host", capturedUri.Host);
    }

    /// <summary>Verifies that PostJson returns the HTTP status code and response headers.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_ReturnsStatusCodeAndHeaders()
    {
        var handler = MockHandler.ForJsonWithHeaders(
            new TestPayload { Id = 1, Name = "test" },
            ("X-Response-Id", "abc"));
        var client = CreateClient(handler);

        var res = await client.TestPostJson<TestPayload, ErrorPayload>("/api/test", new { Id = 1 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.statusCode);
        Assert.Equal("abc", res.responseHeaders.GetValues("X-Response-Id").First());
    }

    /// <summary>Verifies that PostJson throws <see cref="OperationCanceledException"/> when the request times out.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_Timeout_ThrowsOperationCanceled()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", new { Id = 1 }, timeout: TimeSpan.FromMilliseconds(50), cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that PostJson can return the raw response body as a string.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_ResultAsString_ReturnsRawString()
    {
        var handler = MockHandler.ForRawString("raw response");
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<string, string>("/api/test", new { Id = 1 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("raw response", result);
        Assert.Null(error);
    }

    /// <summary>Verifies that PostJson can return the raw response body as a byte array.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_ResultAsBytes_ReturnsRawBytes()
    {
        var expected = new byte[] { 1, 2, 3, 4 };
        var handler = MockHandler.ForRawBytes(expected);
        var client = CreateClient(handler);

        var (result, _) = await client.TestPostJsonAsync<byte[], string>("/api/test", new { Id = 1 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(expected, result);
    }

    /// <summary>Verifies that PostJson returns the raw error body as a string on failure.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_ErrorAsString_ReturnsRawErrorString()
    {
        var handler = MockHandler.ForRawError(HttpStatusCode.InternalServerError, "something broke");
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostJsonAsync<string, string>("/api/test", new { Id = 1 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal("something broke", error);
    }

    #endregion

    #region PostBytes

    /// <summary>Verifies that PostBytesAsync deserializes a successful JSON response into the result type.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostBytesAsync_Success_DeserializesResult()
    {
        var expected = new TestPayload { Id = 2, Name = "bytes" };
        var handler = MockHandler.ForJson(expected);
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01, 0x02], cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
        Assert.Null(error);
    }

    /// <summary>Verifies that PostBytesAsync deserializes an error JSON response into the error type.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostBytesAsync_Error_DeserializesError()
    {
        var handler = MockHandler.ForError(HttpStatusCode.BadRequest, new ErrorPayload { Message = "bad upload" });
        var client = CreateClient(handler);

        var (result, error) = await client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Equal("bad upload", error.Message);
    }

    /// <summary>Verifies that PostBytes forwards additional request headers.</summary>
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
        _ = await client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01], headers: headers, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("upload-123", headerValue);
    }

    /// <summary>Verifies that PostBytes sets the specified content type on the request.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostBytes_SetsContentType()
    {
        string? contentType = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            contentType = req.Content!.Headers.ContentType?.MediaType;
        }, new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01], mediaType: "image/png", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("image/png", contentType);
    }

    /// <summary>Verifies that PostBytes throws <see cref="OperationCanceledException"/> when the request times out.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostBytes_Timeout_ThrowsOperationCanceled()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestPostBytesAsync<TestPayload, ErrorPayload>("/api/upload", [0x01], timeout: TimeSpan.FromMilliseconds(50), cancellationToken: TestContext.Current.CancellationToken));
    }

    #endregion

    #region Get

    /// <summary>Verifies that GetAsync deserializes a successful JSON response into the result type.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_Success_DeserializesResult()
    {
        var expected = new TestPayload { Id = 3, Name = "get" };
        var handler = MockHandler.ForJson(expected);
        var client = CreateClient(handler);

        var (result, error) = await client.TestGetAsync<TestPayload, ErrorPayload>("/api/data", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(3, result.Id);
        Assert.Equal("get", result.Name);
        Assert.Null(error);
    }

    /// <summary>Verifies that GetAsync deserializes an error JSON response into the error type.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_Error_DeserializesError()
    {
        var handler = MockHandler.ForError(HttpStatusCode.NotFound, new ErrorPayload { Message = "not found" });
        var client = CreateClient(handler);

        var (result, error) = await client.TestGetAsync<TestPayload, ErrorPayload>("/api/data", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Equal("not found", error.Message);
    }

    /// <summary>Verifies that GetAsync forwards additional request headers.</summary>
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
        _ = await client.TestGetAsync<TestPayload, ErrorPayload>("/api/data", headers: headers, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Bearer token123", headerValue);
    }

    /// <summary>Verifies that Get returns the HTTP status code and response headers.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task Get_ReturnsStatusCodeAndHeaders()
    {
        var handler = MockHandler.ForJsonWithHeaders(
            new TestPayload { Id = 1, Name = "test" },
            ("X-Total-Count", "42"));
        var client = CreateClient(handler);

        var res = await client.TestGet<TestPayload, ErrorPayload>("/api/data", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.statusCode);
        Assert.Equal("42", res.responseHeaders.GetValues("X-Total-Count").First());
    }

    /// <summary>Verifies that a full URL in GetAsync overrides the client base address.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_FullUrl_OverridesBaseAddress()
    {
        Uri? capturedUri = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            capturedUri = req.RequestUri;
        }, new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await client.TestGetAsync<TestPayload, ErrorPayload>("http://external-api.com/data", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(capturedUri);
        Assert.Equal("external-api.com", capturedUri.Host);
    }

    /// <summary>Verifies that Get throws <see cref="OperationCanceledException"/> when the request times out.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task Get_Timeout_ThrowsOperationCanceled()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestGetAsync<TestPayload, ErrorPayload>("/api/data", timeout: TimeSpan.FromMilliseconds(50), cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that GetAsync can return the raw response body as a string.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_ResultAsString_ReturnsRawString()
    {
        var handler = MockHandler.ForRawString("plain text");
        var client = CreateClient(handler);

        var (result, _) = await client.TestGetAsync<string, string>("/api/data", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("plain text", result);
    }

    /// <summary>Verifies that GetAsync can return the raw response body as a byte array.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task GetAsync_ResultAsBytes_ReturnsRawBytes()
    {
        var expected = new byte[] { 10, 20, 30 };
        var handler = MockHandler.ForRawBytes(expected);
        var client = CreateClient(handler);

        var (result, _) = await client.TestGetAsync<byte[], string>("/api/data", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(expected, result);
    }

    #endregion

    #region CancellationToken

    /// <summary>Verifies that PostJson respects the provided <see cref="CancellationToken"/>.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostJson_CancellationToken_Honored()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestPostJsonAsync<TestPayload, ErrorPayload>("/api/test", cancellationToken: cts.Token));
    }

    /// <summary>Verifies that Get respects the provided <see cref="CancellationToken"/>.</summary>
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

    #region PostMultipart

    private static MultipartFormDataContent CreateMultipart(string fieldValue = "json")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(file, "file", "audio.wav");
        content.Add(new StringContent(fieldValue), "response_format");
        return content;
    }

    /// <summary>Verifies that PostMultipartAsync deserializes a successful JSON response into the result type.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostMultipartAsync_Success_DeserializesResult()
    {
        var handler = MockHandler.ForJson(new TestPayload { Id = 7, Name = "transcript" });
        var client = CreateClient(handler);
        using var content = CreateMultipart();

        var (result, error) = await client.TestPostMultipartAsync<TestPayload, ErrorPayload>(
            "/api/test", content, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("transcript", result.Name);
        Assert.Null(error);
    }

    /// <summary>Verifies that PostMultipartAsync deserializes an error JSON response into the error type.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostMultipartAsync_Error_DeserializesError()
    {
        var handler = MockHandler.ForError(HttpStatusCode.UnsupportedMediaType, new ErrorPayload { Message = "nope" });
        var client = CreateClient(handler);
        using var content = CreateMultipart();

        var (result, error) = await client.TestPostMultipartAsync<TestPayload, ErrorPayload>(
            "/api/test", content, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Equal("nope", error.Message);
    }

    /// <summary>Verifies that the multipart body reaches the server with its parts and boundary intact.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostMultipart_SendsPartsAndBoundary()
    {
        string? capturedBody = null;
        string? capturedContentType = null;
        var handler = MockHandler.WithCapture(async req =>
        {
            capturedContentType = req.Content!.Headers.ContentType?.MediaType;
            capturedBody = await req.Content.ReadAsStringAsync();
        }, new TestPayload { Id = 1, Name = "ok" });
        var client = CreateClient(handler);
        using var content = CreateMultipart();

        await client.TestPostMultipartAsync<TestPayload, ErrorPayload>(
            "/api/test", content, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("multipart/form-data", capturedContentType);
        Assert.NotNull(capturedBody);
        //The part names are what a transcription backend reads the audio and options from.
        Assert.Contains("name=file", capturedBody, StringComparison.Ordinal);
        Assert.Contains("filename=audio.wav", capturedBody, StringComparison.Ordinal);
        Assert.Contains("name=response_format", capturedBody, StringComparison.Ordinal);
    }

    /// <summary>Verifies that PostMultipart forwards additional request headers.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostMultipart_WithHeaders_SendsHeaders()
    {
        string? captured = null;
        var handler = MockHandler.WithCapture(req =>
        {
            captured = req.Headers.TryGetValues("X-Test", out var values) ? values.First() : null;
            return Task.CompletedTask;
        }, new TestPayload { Id = 1, Name = "ok" });
        var client = CreateClient(handler);
        using var content = CreateMultipart();

        await client.TestPostMultipartAsync<TestPayload, ErrorPayload>("/api/test", content,
            headers: [("X-Test", "abc")], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("abc", captured);
    }

    /// <summary>Verifies that PostMultipart surfaces the status code and response headers.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostMultipart_ReturnsStatusCodeAndHeaders()
    {
        var handler = MockHandler.ForJsonWithHeaders(new TestPayload { Id = 1, Name = "ok" }, ("X-Trace", "xyz"));
        var client = CreateClient(handler);
        using var content = CreateMultipart();

        var (result, _, statusCode, responseHeaders) = await client.TestPostMultipart<TestPayload, ErrorPayload>(
            "/api/test", content, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Equal("xyz", responseHeaders.GetValues("X-Trace").First());
    }

    /// <summary>Verifies that a byte[] result type returns the raw response body, as audio endpoints need.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostMultipart_ResultAsBytes_ReturnsRawBytes()
    {
        byte[] expected = [0x4F, 0x67, 0x67, 0x53];
        var handler = MockHandler.ForRawBytes(expected);
        var client = CreateClient(handler);
        using var content = CreateMultipart();

        var (result, error) = await client.TestPostMultipartAsync<byte[], string>(
            "/api/test", content, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(expected, result);
        Assert.Null(error);
    }

    /// <summary>Verifies that the per-request timeout cancels a slow multipart request.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostMultipart_Timeout_ThrowsOperationCanceled()
    {
        var handler = MockHandler.WithDelay(TimeSpan.FromSeconds(10), new TestPayload { Id = 1, Name = "test" });
        var client = CreateClient(handler);
        using var content = CreateMultipart();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestPostMultipartAsync<TestPayload, ErrorPayload>("/api/test", content,
                timeout: TimeSpan.FromMilliseconds(50), cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that an absolute request URI bypasses the configured base address.</summary>
    [Fact, Trait("Category", "HttpClientBase")]
    public async Task PostMultipart_FullUrl_OverridesBaseAddress()
    {
        Uri? captured = null;
        var handler = MockHandler.WithCapture(req =>
        {
            captured = req.RequestUri;
            return Task.CompletedTask;
        }, new TestPayload { Id = 1, Name = "ok" });
        var client = CreateClient(handler);
        using var content = CreateMultipart();

        await client.TestPostMultipartAsync<TestPayload, ErrorPayload>(
            "https://elsewhere/api/test", content, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("https://elsewhere/api/test", captured?.ToString());
    }

    #endregion
}
