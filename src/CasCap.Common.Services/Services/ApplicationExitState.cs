namespace CasCap.Common.Services;

/// <summary>Tracks the process result that the application host returns after graceful shutdown.</summary>
public sealed class ApplicationExitState
{
    private int _exitCode;

    /// <summary>Gets the process exit code. Zero represents success; nonzero represents failure.</summary>
    public int ExitCode => Volatile.Read(ref _exitCode);

    /// <summary>Marks the application as failed without bypassing host shutdown or disposal.</summary>
    public void MarkFailure() => Interlocked.Exchange(ref _exitCode, 1);
}
