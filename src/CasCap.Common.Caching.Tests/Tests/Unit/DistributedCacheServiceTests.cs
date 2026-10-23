using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CasCap.Common.Caching.Tests;

/// <summary>Verifies cache-miss population and stampede suppression in <see cref="DistributedCacheService"/> without Redis.</summary>
[Trait("Category", nameof(IDistributedCache))]
public sealed class DistributedCacheServiceTests
{
    private const int ConcurrentCallers = 5;
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    //Regression: a distributed lock not acquired within its wait time was reported to the caller as a cache miss.
    [Fact]
    public async Task GetMiss_CreatesEntryWhenDistributedLockTimesOut()
    {
        var lease = new TestLease();
        lease.Lose();
        var factory = new TestLockFactory((_, _) => Task.FromResult<IRedLock>(lease));
        var sut = CreateService(factory);
        var key = $"{Guid.NewGuid()}:{nameof(GetMiss_CreatesEntryWhenDistributedLockTimesOut)}";
        var expected = new MockDto(DateTime.UtcNow);

        var created = await sut.Get(key, () => Task.FromResult(expected));
        var cached = await sut.Get<MockDto>(key);

        Assert.Equal(expected, created);
        Assert.Equal(expected, cached);
        Assert.Equal(1, factory.Attempts);
        Assert.True(lease.Disposed);
    }

    //Regression: callers queued behind the per-key lock re-ran the factory instead of reading the populated entry.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetMiss_RunsFactoryOnceForConcurrentCallers(bool distributedLockingEnabled)
    {
        var factory = distributedLockingEnabled
            ? new TestLockFactory((_, _) => Task.FromResult<IRedLock>(new TestLease()))
            : null;
        var sut = CreateService(factory);
        var key = $"{Guid.NewGuid()}:{nameof(GetMiss_RunsFactoryOnceForConcurrentCallers)}";
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        async Task<MockDto> CreateItem()
        {
            Interlocked.Increment(ref factoryCalls);
            await release.Task;
            return new MockDto(DateTime.UtcNow);
        }

        //Each call runs synchronously until it awaits the release gate or the per-key lock, so every caller misses first.
        var callers = Enumerable.Range(0, ConcurrentCallers).Select(_ => sut.Get(key, CreateItem)).ToArray();
        release.SetResult();
        var results = await Task.WhenAll(callers).WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(1, factoryCalls);
        Assert.All(results, result => Assert.Equal(results[0], result));
        Assert.Equal(distributedLockingEnabled ? 1 : 0, factory?.Attempts ?? 0);
    }

    //Regression: the local copy ignored the absolute expiration and outlived the remote entry indefinitely.
    [Fact]
    public async Task Set_AppliesAbsoluteExpirationToLocalCache()
    {
        var localCache = new RecordingLocalCache();
        var sut = CreateService(lockFactory: null, localCache);
        var key = $"{Guid.NewGuid()}:{nameof(Set_AppliesAbsoluteExpirationToLocalCache)}";
        var expiration = DateTimeOffset.UtcNow.AddMinutes(5);

        await sut.Set(key, new MockDto(DateTime.UtcNow), absoluteExpiration: expiration);

        Assert.Equal(expiration, localCache.AbsoluteExpiration);
    }

    private static DistributedCacheService CreateService(IDistributedLockFactory? lockFactory, ILocalCache? localCache = null)
    {
        var cachingConfig = Options.Create(new CachingConfig
        {
            DistributedLockingEnabled = lockFactory is not null,
            RemoteCache = new CacheParameters { IsEnabled = false }
        });
        return new(NullLogger<DistributedCacheService>.Instance, cachingConfig, Options.Create(new RedlockConfig()),
            new NullRemoteCache(cachingConfig), localCache ?? new MemoryCacheService(NullLogger<MemoryCacheService>.Instance, cachingConfig),
            lockFactory);
    }

    private sealed class RecordingLocalCache : ILocalCache
    {
        public DateTimeOffset? AbsoluteExpiration { get; private set; }

        public void Set<T>(string key, T cacheEntry, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null) =>
            AbsoluteExpiration = absoluteExpiration;

        public T? Get<T>(string key) => throw new NotSupportedException();

        public bool Delete(string key) => throw new NotSupportedException();

        public long DeleteAll() => throw new NotSupportedException();
    }
}
