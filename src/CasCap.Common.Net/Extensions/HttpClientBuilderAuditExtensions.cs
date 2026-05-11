#if NET8_0_OR_GREATER
using CasCap.Common.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CasCap.Common.Extensions;

/// <summary>Extension methods for adding HTTP audit logging to <see cref="IHttpClientBuilder"/> registrations.</summary>
public static class HttpClientBuilderAuditExtensions
{
    /// <summary>
    /// Adds the <see cref="HttpAuditHandler"/> to the HTTP client pipeline with a logical source name.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    /// <param name="sourceName">
    /// A display name for the calling service (typically <c>nameof(MyService)</c>)
    /// stored as <see cref="HttpAuditEntry.Source"/>.
    /// </param>
    /// <returns>The <see cref="IHttpClientBuilder"/> for further chaining.</returns>
    public static IHttpClientBuilder AddHttpAuditing(this IHttpClientBuilder builder, string sourceName)
    {
        builder.Services.TryAddSingleton<IHttpAuditStore, NullHttpAuditStore>();
        builder.Services.AddTransient<HttpAuditHandler>();
        builder.AddHttpMessageHandler(() => new HttpAuditSourceHandler(sourceName));
        builder.AddHttpMessageHandler<HttpAuditHandler>();
        return builder;
    }

    /// <summary>Internal handler that stamps each outgoing request with the logical source name.</summary>
    private sealed class HttpAuditSourceHandler(string sourceName) : DelegatingHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Options.Set(HttpAuditSource.Key, sourceName);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
#endif
