namespace CasCap.Common.Caching.Tests;

/// <summary>In-memory <see cref="IRedLock"/> that starts acquired and can be lost or faulted on demand.</summary>
internal sealed class TestLease : IRedLock
{
    private int _acquired = 1;
    private Exception? _observationFault;

    /// <summary>Gets the exception thrown when the lease is disposed.</summary>
    public Exception? DisposalFault { get; init; }

    /// <summary>Gets whether the lease has been disposed.</summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc/>
    public string Resource => "worker";

    /// <inheritdoc/>
    public string LockId => "test-lock";

    /// <inheritdoc/>
    public bool IsAcquired => Volatile.Read(ref _observationFault) is { } fault
        ? throw fault : Volatile.Read(ref _acquired) == 1;

    /// <inheritdoc/>
    public RedLockStatus Status => IsAcquired ? RedLockStatus.Acquired : RedLockStatus.Expired;

    /// <inheritdoc/>
    public RedLockInstanceSummary InstanceSummary => new(1, 0, 0);

    /// <inheritdoc/>
    public int ExtendCount => 0;

    /// <summary>Marks the lease as no longer acquired.</summary>
    public void Lose() => Interlocked.Exchange(ref _acquired, 0);

    /// <summary>Makes every subsequent <see cref="IsAcquired"/> read throw <paramref name="fault"/>.</summary>
    public void FailObservation(Exception fault) => Volatile.Write(ref _observationFault, fault);

    /// <inheritdoc/>
    public void Dispose()
    {
        Disposed = true;
        Lose();
        if (DisposalFault is { } fault)
            throw fault;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
