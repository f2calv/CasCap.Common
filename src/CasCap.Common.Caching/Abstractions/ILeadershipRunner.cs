#if NET8_0_OR_GREATER
namespace CasCap.Common.Abstractions;

/// <summary>Runs a long-lived worker only while holding a distributed leadership lease.</summary>
public interface ILeadershipRunner
{
    /// <summary>Contends for ownership and awaits each worker's cleanup before releasing or reacquiring.</summary>
    /// <param name="resource">Stable logical resource shared by all contenders; the factory applies its key format.</param>
    /// <param name="runAsync">Long-lived work that observes its token and awaits all child work and cleanup.</param>
    /// <param name="cancellationToken">Stops acquisition and the current ownership run.</param>
    /// <remarks>
    /// Lease loss cancels the worker and permits reacquisition only after it stops. Unexpected worker completion
    /// or failure terminates the runner. Cleanup is awaited without a timeout; the host owns forced termination.
    /// Polling a lease is not downstream fencing or an exactly-once guarantee.
    /// </remarks>
    public Task RunAsync(string resource, Func<CancellationToken, Task> runAsync, CancellationToken cancellationToken);
}
#endif
