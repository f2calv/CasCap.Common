using CasCap.Common.Models;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace CasCap.Common.Net.Tests;

/// <summary>
/// Tests the replay-safety predicate that stops a non-idempotent request being retried after the
/// server may already have processed it.
/// </summary>
[Trait("Category", "Resilience")]
public class HttpClientBuilderResilienceExtensionTests
{
    public static TheoryData<string> IdempotentMethods() => ["GET", "HEAD", "PUT", "DELETE", "OPTIONS", "TRACE"];

    public static TheoryData<string> NonIdempotentMethods() => ["POST", "PATCH"];

    [Theory, MemberData(nameof(IdempotentMethods))]
    public void IdempotentMethod_IsReplaySafe_AfterTimeout(string method)
    {
        //A lost response cannot duplicate a side effect when replaying the request is harmless.
        Assert.True(Evaluate(method, new TimeoutRejectedException()));
    }

    [Theory, MemberData(nameof(NonIdempotentMethods))]
    public void NonIdempotentMethod_IsNotReplaySafe_AfterTimeout(string method)
    {
        //The server may have completed the work and only the response was lost.
        Assert.False(Evaluate(method, new TimeoutRejectedException()));
    }

    [Theory, MemberData(nameof(NonIdempotentMethods))]
    public void NonIdempotentMethod_IsNotReplaySafe_AfterServerError(string method) =>
        Assert.False(Evaluate(method, outcome: new HttpResponseMessage(HttpStatusCode.InternalServerError)));

    [Theory]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.NameResolutionError)]
    [InlineData(HttpRequestError.SecureConnectionError)]
    [InlineData(HttpRequestError.ProxyTunnelError)]
    public void NonIdempotentMethod_IsReplaySafe_WhenTheRequestNeverArrived(HttpRequestError error) =>
        Assert.True(Evaluate("POST", new HttpRequestException(error)));

    [Fact]
    public void NonIdempotentMethod_IsNotReplaySafe_WhenTheRequestWasTransmitted() =>
        Assert.False(Evaluate("POST", new HttpRequestException(HttpRequestError.ResponseEnded)));

    [Fact]
    public void UnknownRequest_IsTreatedAsReplaySafe()
    {
        //Without a request the pipeline cannot prove the call is unsafe, so the standard predicate decides.
        var context = ResilienceContextPool.Shared.Get(TestContext.Current.CancellationToken);
        try
        {
            var args = new RetryPredicateArguments<HttpResponseMessage>(
                context, Outcome.FromException<HttpResponseMessage>(new TimeoutRejectedException()), 0);
            Assert.True(HttpClientBuilderResilienceExtensions.IsReplaySafe(args));
        }
        finally { ResilienceContextPool.Shared.Return(context); }
    }

    #region Private helpers

    private static bool Evaluate(string method, Exception? exception = null, HttpResponseMessage? outcome = null)
    {
        var context = ResilienceContextPool.Shared.Get(TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://example.com/resource");
        context.SetRequestMessage(request);
        try
        {
            var result = exception is not null
                ? Outcome.FromException<HttpResponseMessage>(exception)
                : Outcome.FromResult(outcome!);
            return HttpClientBuilderResilienceExtensions.IsReplaySafe(
                new RetryPredicateArguments<HttpResponseMessage>(context, result, 0));
        }
        finally
        {
            outcome?.Dispose();
            ResilienceContextPool.Shared.Return(context);
        }
    }

    #endregion
}
