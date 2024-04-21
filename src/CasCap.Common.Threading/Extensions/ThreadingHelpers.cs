using AsyncKeyedLock;

namespace CasCap.Common.Extensions;

public static class ThreadingHelpers
{
#if NET6_0_OR_GREATER
    [Obsolete("Use Parallel.ForEachAsync instead")]
#endif
    public static Task<TimeSpan> ForEachAsyncSemaphore<T>(this IEnumerable<T> source, Func<T, Task> body) => source.ForEachAsyncSemaphore(body, Environment.ProcessorCount);

    /// <summary>
    /// throttling asynchronous methods
    /// </summary>
#if NET6_0_OR_GREATER
    [Obsolete("Use Parallel.ForEachAsync instead")]
#endif
    public static async Task<TimeSpan> ForEachAsyncSemaphore<T>(this IEnumerable<T> source, Func<T, Task> body, int degreeOfParallelism)
    {
        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>();
        using var throttler = new AsyncNonKeyedLocker();
        foreach (var element in source)
        {
#pragma warning disable CAC001 // ConfigureAwaitChecker
            using (await throttler.LockAsync())
#pragma warning restore CAC001 // ConfigureAwaitChecker
            {
                tasks.Add(Task.Run(async () =>
                {
#pragma warning disable CAC001 // ConfigureAwaitChecker
                    await body(element);
#pragma warning restore CAC001 // ConfigureAwaitChecker
                }));
            }
#pragma warning disable CAC001 // ConfigureAwaitChecker
            await Task.WhenAll(tasks);
#pragma warning restore CAC001 // ConfigureAwaitChecker
        }
        return sw.Elapsed;
    }

#if NET6_0_OR_GREATER
    [Obsolete("Use Parallel.ForEachAsync instead")]
#endif
        public static async Task<TimeSpan> ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> body)
        {
            var sw = Stopwatch.StartNew();
            foreach (var element in source)
#pragma warning disable CAC001 // ConfigureAwaitChecker
                await body(element);
#pragma warning restore CAC001 // ConfigureAwaitChecker
            return sw.Elapsed;
        }
    }