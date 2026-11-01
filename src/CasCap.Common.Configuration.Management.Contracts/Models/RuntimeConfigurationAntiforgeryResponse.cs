namespace CasCap.Common.Configuration.Management.Contracts.Models;

/// <summary>A browser request token paired with the server's HttpOnly antiforgery cookie.</summary>
/// <param name="HeaderName">The header to attach on POST requests. <example>RequestVerificationToken</example></param>
/// <param name="RequestToken">The principal-bound request token. <example>opaque-token</example></param>
public sealed record RuntimeConfigurationAntiforgeryResponse(string HeaderName, string RequestToken);
