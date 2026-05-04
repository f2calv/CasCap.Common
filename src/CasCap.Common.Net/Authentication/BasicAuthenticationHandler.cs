#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace CasCap.Authentication;

/// <summary>
/// ASP.NET Core authentication handler that validates HTTP Basic credentials
/// against the <see cref="ApiAuthConfig"/> settings.
/// </summary>
public class BasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiAuthConfig> config) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The authentication scheme name used for HTTP Basic authentication.</summary>
    public const string SchemeName = "Basic";

    private readonly ApiAuthConfig _apiAuthConfig = config.Value;

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith($"{SchemeName} ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogDebug(
                "{ClassName} authentication header missing or not Basic for {Method} {Path}",
                nameof(BasicAuthenticationHandler), Request.Method, Request.Path);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        try
        {
            var encodedCredentials = authHeader[$"{SchemeName} ".Length..].Trim();
            var decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
            var separatorIndex = decodedCredentials.IndexOf(':');
            if (separatorIndex < 0)
            {
                Logger.LogDebug(
                    "{ClassName} invalid Basic header format for {Method} {Path}",
                    nameof(BasicAuthenticationHandler), Request.Method, Request.Path);
                return Task.FromResult(AuthenticateResult.Fail("Invalid Basic authentication header format."));
            }

            var username = decodedCredentials[..separatorIndex];
            var password = decodedCredentials[(separatorIndex + 1)..];

            if (username != _apiAuthConfig.Username || password != _apiAuthConfig.Password)
            {
                Logger.LogDebug(
                    "{ClassName} invalid credentials for {Method} {Path} with {SuppliedUsername}",
                    nameof(BasicAuthenticationHandler), Request.Method, Request.Path, username);
                return Task.FromResult(AuthenticateResult.Fail("Invalid username or password."));
            }

            Logger.LogDebug(
                "{ClassName} authenticated {Username} for {Method} {Path}",
                nameof(BasicAuthenticationHandler), username, Request.Method, Request.Path);

            var claims = new[] { new Claim(ClaimTypes.Name, username) };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (FormatException)
        {
            Logger.LogDebug(
                "{ClassName} invalid Base64 in Basic header for {Method} {Path}",
                nameof(BasicAuthenticationHandler), Request.Method, Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("Invalid Base64 encoding in Basic authentication header."));
        }
    }

    /// <inheritdoc/>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = SchemeName;
        return base.HandleChallengeAsync(properties);
    }
}
#endif
