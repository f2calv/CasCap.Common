namespace CasCap.Common.Caching.Tests;

/// <summary>
/// Mock API for testing fake async data retrieval.
/// </summary>
public class MockApiService
{
    private static MockDto? obj = null;

    /// <summary>
    /// Mock synchronous data retrieval.
    /// </summary>
    public static MockDto Get()
    {
        //lets go fake getting some data
        obj ??= new MockDto(DateTime.UtcNow);
        return obj;
    }

    /// <summary>
    /// Mock asynchronous data retrieval.
    /// </summary>
    public static Task<MockDto> GetAsync()
    {
        //lets go fake getting some data
        obj ??= new MockDto(DateTime.UtcNow);
        return Task.FromResult(obj);
    }
}
