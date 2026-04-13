namespace CasCap.Common.Caching.Tests;

/// <summary>
/// Test DTO for cache serialization round-trip tests.
/// </summary>
[MessagePackObject(true)]
public class MockDto(DateTime someDateTimeUtc)
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public int SomeId { get; init; } = 1337;

    /// <summary>
    /// Test property.
    /// </summary>
    public DateTime SomeDateTimeUtc { get; init; } = someDateTimeUtc;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is MockDto @class &&
               SomeId == @class.SomeId &&
               SomeDateTimeUtc == @class.SomeDateTimeUtc;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(SomeId, SomeDateTimeUtc);

    /// <inheritdoc/>
    public override string? ToString() => $"{SomeId} {SomeDateTimeUtc}";
}
