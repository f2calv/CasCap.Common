namespace CasCap.Common.Net.Tests;

using System.Text;
using System.Text.Json;

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

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request, cancellationToken);

    /// <summary>
    /// Creates a handler that returns a JSON-serialized payload.
    /// </summary>
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

    /// <summary>
    /// Creates a handler that returns a JSON-serialized payload with custom response headers.
    /// </summary>
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

    /// <summary>
    /// Creates a handler that returns a JSON-serialized error payload with the specified status code.
    /// </summary>
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

    /// <summary>
    /// Creates a handler that returns a raw string response.
    /// </summary>
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

    /// <summary>
    /// Creates a handler that returns a raw string error response.
    /// </summary>
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

    /// <summary>
    /// Creates a handler that returns raw bytes.
    /// </summary>
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

    /// <summary>
    /// Creates a handler that invokes a capture callback before returning a JSON response.
    /// </summary>
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

    /// <summary>
    /// Creates a handler that delays before returning a JSON response.
    /// </summary>
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
