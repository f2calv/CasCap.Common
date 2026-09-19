#if NET8_0_OR_GREATER
namespace CasCap.Common.Models;

/// <summary>Which requests a resilience pipeline is permitted to retry.</summary>
/// <remarks>
/// Retry safety is a property of the request, not of the client. Replaying a request the server may
/// already have processed duplicates its side effect — a second Signal message, or a second trade.
/// </remarks>
public enum HttpRetrySafety
{
    /// <summary>
    /// Retry idempotent methods normally, and retry a non-idempotent method only when the request
    /// provably never reached the server. This is the default.
    /// </summary>
    SafeMethodsOnly = 0,

    /// <summary>
    /// Retry every method on any transient failure, including a timeout.
    /// </summary>
    /// <remarks>
    /// Only choose this where the endpoint is genuinely idempotent, for example because it accepts
    /// an idempotency key. A timeout does not prove the server ignored the request.
    /// </remarks>
    AllMethods = 1,

    /// <summary>Never retry.</summary>
    Never = 2,
}
#endif
