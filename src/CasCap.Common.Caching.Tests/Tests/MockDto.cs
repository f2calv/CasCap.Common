namespace CasCap.Common.Caching.Tests;

/// <summary>
/// Test DTO for cache serialization round-trip tests.
/// </summary>
[MessagePackObject(true)]
public class MockDto
{
    /// <summary>
    /// Initializes a new instance with the specified UTC timestamp.
    /// </summary>
    public MockDto(DateTime someDateTimeUtc)
    {
        SomeDateTimeUtc = someDateTimeUtc;
        SomeId = 1337;
    }

    /// <summary>
    /// Unique identifier.
    /// </summary>
    public int SomeId { get; init; }

    /// <summary>
    /// Test property.
    /// </summary>
    public DateTime SomeDateTimeUtc { get; init; }

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
