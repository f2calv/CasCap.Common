#if NET8_0_OR_GREATER
namespace CasCap.Common.Auditing;

/// <summary>Defines the <see cref="HttpRequestOptionsKey{TValue}"/> used to tag requests with a logical source name.</summary>
public static class HttpAuditSource
{
    /// <summary>The options key used to pass the source name through <see cref="HttpRequestMessage.Options"/>.</summary>
    public static readonly HttpRequestOptionsKey<string> Key = new("HttpAuditSource");
}
#endif
