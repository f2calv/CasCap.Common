namespace CasCap.Common.Configuration.Blazor.Constants;

/// <summary>Typed client outcomes; no operation silently converts an error to a document.</summary>
public static class RuntimeConfigurationClientStatus
{
    /// <summary>A valid successful response.</summary>
    public const string Success = nameof(Success);
    /// <summary>The request or candidate failed validation.</summary>
    public const string Validation = nameof(Validation);
    /// <summary>The host requires sign-in.</summary>
    public const string Unauthorized = nameof(Unauthorized);
    /// <summary>The principal lacks the requested permission.</summary>
    public const string Forbidden = nameof(Forbidden);
    /// <summary>The configured destination or document was not found.</summary>
    public const string NotFound = nameof(NotFound);
    /// <summary>The expected revision no longer matches.</summary>
    public const string Conflict = nameof(Conflict);
    /// <summary>Storage or transport failed; a mutation outcome may be uncertain.</summary>
    public const string Unavailable = nameof(Unavailable);
    /// <summary>The server returned an unexpected response.</summary>
    public const string InvalidResponse = nameof(InvalidResponse);
}
