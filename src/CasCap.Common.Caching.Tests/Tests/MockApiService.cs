namespace CasCap.Common.Caching.Tests;

/// <summary>Mock API for testing fake async data retrieval.</summary>
public static class MockApiService
{
    /// <summary>Mock synchronous data retrieval.</summary>
    public static MockDto Get() => new(DateTime.UtcNow);

    /// <summary>Mock asynchronous data retrieval.</summary>
    public static Task<MockDto> GetAsync() => Task.FromResult(new MockDto(DateTime.UtcNow));
}
