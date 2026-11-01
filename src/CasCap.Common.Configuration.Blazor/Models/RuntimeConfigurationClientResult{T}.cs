namespace CasCap.Common.Configuration.Blazor.Models;

/// <summary>A typed HTTP outcome with safe user-facing text and optional server validation fields.</summary>
public sealed record RuntimeConfigurationClientResult<T> where T : class
{
    /// <summary>Gets the outcome from <see cref="RuntimeConfigurationClientStatus"/>.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the successful value, never a fallback for an error.</summary>
    public T? Value { get; init; }

    /// <summary>Gets a fixed user-safe explanation.</summary>
    public required string Message { get; init; }

    /// <summary>Gets validation messages supplied by the authorized API.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    /// <summary>Gets whether a valid successful value was received.</summary>
    public bool Succeeded => Status == RuntimeConfigurationClientStatus.Success && Value is not null;
}
