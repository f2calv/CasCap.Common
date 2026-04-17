#if NET8_0_OR_GREATER
using System.ComponentModel.DataAnnotations;

namespace CasCap.Common.Abstractions;

/// <summary>API basic authentication configuration.</summary>
public record ApiAuthConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(ApiAuthConfig)}";

    /// <summary>
    /// Standard kubernetes API basic authentication username.
    /// </summary>
    [Required, MinLength(1)]
    public required string Username { get; init; }

    /// <summary>
    /// Standard kubernetes API basic authentication password.
    /// </summary>
    [Required, MinLength(1)]
    public required string Password { get; init; }
}
#endif
