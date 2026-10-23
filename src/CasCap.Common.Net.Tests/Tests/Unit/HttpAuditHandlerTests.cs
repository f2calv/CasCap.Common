using CasCap.Common.Abstractions;
using CasCap.Common.Auditing;
using System.Text.Json;

namespace CasCap.Common.Net.Tests;

/// <summary>Tests JSON-safe audit capture without changing HTTP content or persistence-failure behavior.</summary>
[Trait("Category", "Auditing")]
public sealed class HttpAuditHandlerTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" \t\r\n", false)]
    [InlineData(" { \"value\": 1 } ", true)]
    [InlineData("[1,true,null]", true)]
    [InlineData("\"\"", true)]
    [InlineData("\"text\"", true)]
    [InlineData("123.5", true)]
    [InlineData("false", true)]
    [InlineData("null", true)]
    [InlineData("plain text", false)]
    [InlineData("<html>unavailable</html>", false)]
    [InlineData("{\"truncated\":", false)]
    public async Task CapturesJsonSafeBodiesAndPreservesHttpContent(string? body, bool validJson)
    {
        // Regression: empty and non-JSON HTTP bodies must not be sent to JSON stores as invalid JSON.
        var store = new RecordingStore();
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        if (body is not null)
            response.Content = new StringContent(body);
        using var client = CreateClient(store, response);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/path?q=value");
        request.Options.Set(HttpAuditSource.Key, "ExampleClient");
        if (body is not null)
            request.Content = new StringContent(body);

        using var actual = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Same(response, actual);
        Assert.Equal(body ?? "", await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        if (request.Content is not null)
            Assert.Equal(body, await request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var entry = Assert.Single(store.Entries);
        Assert.Equal("ExampleClient", entry.Source);
        Assert.Equal("/path?q=value", entry.RequestUri);
        Assert.Equal("POST", entry.HttpMethod);
        Assert.Equal(200, entry.StatusCode);
        Assert.Equal(entry.RequestBody, entry.ResponseBody);
        if (string.IsNullOrWhiteSpace(body))
            Assert.Null(entry.ResponseBody);
        else if (validJson)
            Assert.Equal(body, entry.ResponseBody);
        else
        {
            Assert.NotNull(entry.ResponseBody);
            using var json = JsonDocument.Parse(entry.ResponseBody);
            Assert.Equal(JsonValueKind.String, json.RootElement.ValueKind);
            Assert.Equal(body, json.RootElement.GetString());
        }
    }

    [Fact]
    public async Task DeepValidJson_RetainsItsOriginalRepresentation()
    {
        var body = new string('[', 80) + "0" + new string(']', 80);
        var store = new RecordingStore();
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        using var client = CreateClient(store, response);

        using var actual = await client.GetAsync("https://example.com/path", TestContext.Current.CancellationToken);

        Assert.Equal(body, Assert.Single(store.Entries).ResponseBody);
        Assert.Equal(body, await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EmptyNoContentResponse_CapturesNullAndPreservesStatus()
    {
        var store = new RecordingStore();
        using var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        using var client = CreateClient(store, response);

        using var actual = await client.GetAsync("https://example.com/path", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, actual.StatusCode);
        var entry = Assert.Single(store.Entries);
        Assert.Null(entry.RequestBody);
        Assert.Null(entry.ResponseBody);
        Assert.Equal(204, entry.StatusCode);
        Assert.Equal("Unknown", entry.Source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistenceFailure_IsLoggedAndDoesNotReplaceTheHttpResponse(bool cancelledStore)
    {
        var failure = cancelledStore ? new OperationCanceledException() : (Exception)new IOException("Synthetic store failure.");
        var store = new RecordingStore { Failure = failure };
        var logger = new RecordingLogger();
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream unavailable"),
        };
        using var client = CreateClient(store, response, logger);

        using var actual = await client.GetAsync("https://example.com/path", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, actual.StatusCode);
        Assert.Equal("upstream unavailable", await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var warning = Assert.Single(logger.Warnings);
        Assert.Same(failure, warning);
        Assert.Single(store.Entries);
    }

    [Fact]
    public async Task CallerCancellation_PreventsSendingAndSaving()
    {
        var store = new RecordingStore();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        using var client = CreateClient(store, response);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync("https://example.com/path", cancellation.Token));

        Assert.Empty(store.Entries);
    }

    private static HttpClient CreateClient(RecordingStore store, HttpResponseMessage response,
        ILogger<HttpAuditHandler>? logger = null)
        => new(new HttpAuditHandler(store, logger ?? new RecordingLogger(), TimeProvider.System)
        {
            InnerHandler = new ResponseHandler(response),
        });

    private sealed class ResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingStore : IHttpAuditStore
    {
        public List<HttpAuditEntry> Entries { get; } = [];

        public Exception? Failure { get; init; }

        public ValueTask SaveAsync(HttpAuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Failure is not null ? ValueTask.FromException(Failure) : ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingLogger : ILogger<HttpAuditHandler>
    {
        public List<Exception?> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel is LogLevel.Warning)
                Warnings.Add(exception);
        }
    }
}
