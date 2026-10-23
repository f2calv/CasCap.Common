namespace CasCap.Common.Caching.Tests;

/// <summary>Asynchronous <see cref="IDistributedLockFactory"/> that delegates each numbered attempt to a callback.</summary>
internal sealed class TestLockFactory(Func<int, CancellationToken, Task<IRedLock>> acquire) : IDistributedLockFactory
{
    private int _attempts;

    /// <summary>Gets the number of lock attempts made.</summary>
    public int Attempts => Volatile.Read(ref _attempts);

    /// <summary>Gets the resource requested by the latest attempt.</summary>
    public string? Resource { get; private set; }

    /// <summary>Gets the expiry requested by the latest attempt.</summary>
    public TimeSpan Expiry { get; private set; }

    /// <summary>Gets the wait time requested by the latest attempt.</summary>
    public TimeSpan Wait { get; private set; }

    /// <summary>Gets the retry interval requested by the latest attempt.</summary>
    public TimeSpan Retry { get; private set; }

    /// <inheritdoc/>
    public Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime, TimeSpan waitTime,
        TimeSpan retryTime, CancellationToken? cancellationToken = null)
    {
        Resource = resource;
        Expiry = expiryTime;
        Wait = waitTime;
        Retry = retryTime;
        return acquire(Interlocked.Increment(ref _attempts), cancellationToken ?? CancellationToken.None);
    }

    /// <inheritdoc/>
    public Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime)
        => CreateLockAsync(resource, expiryTime, TimeSpan.Zero, TimeSpan.Zero);

    /// <inheritdoc/>
    public IRedLock CreateLock(string resource, TimeSpan expiryTime) => throw new NotSupportedException();

    /// <inheritdoc/>
    public IRedLock CreateLock(string resource, TimeSpan expiryTime, TimeSpan waitTime,
        TimeSpan retryTime, CancellationToken? cancellationToken = null) => throw new NotSupportedException();
}
