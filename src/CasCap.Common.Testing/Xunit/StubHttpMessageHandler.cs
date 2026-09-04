using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CasCap.Common.Xunit;

/// <summary>Returns canned responses so tests can exercise HTTP code paths without a network.</summary>
/// <remarks>
/// Assign to <see cref="System.Net.Http.HttpClient"/> directly, or set <see cref="DelegatingHandler.InnerHandler"/>
/// on the handler under test so it forwards here instead of to the network.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

    /// <summary>Initializes a handler from an asynchronous response factory.</summary>
    /// <param name="responseFactory">Produces the response for each intercepted request.</param>
    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        => _responseFactory = responseFactory;

    /// <summary>Initializes a handler from a synchronous response factory.</summary>
    /// <param name="responseFactory">Produces the response for each intercepted request.</param>
    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        => _responseFactory = (request, _) => Task.FromResult(responseFactory(request));

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _responseFactory(request, cancellationToken);
}
