namespace CasCap.Common.Extensions;

public static class ThreadingHelpers
{
    [Obsolete("Use Parallel.ForEachAsync instead")]
    public static Task<TimeSpan> ForEachAsyncSemaphore<T>(this IEnumerable<T> source, Func<T, Task> body) => source.ForEachAsyncSemaphore(body, Environment.ProcessorCount);

    /// <summary>
    /// throttling asynchronous methods
    /// </summary>
    [Obsolete("Use Parallel.ForEachAsync instead")]
    public static async Task<TimeSpan> ForEachAsyncSemaphore<T>(this IEnumerable<T> source, Func<T, Task> body, int degreeOfParallelism)
    {
        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>();
        using (var throttler = new SemaphoreSlim(degreeOfParallelism))
        {
            foreach (var element in source)
            {
#pragma warning disable CAC001 // ConfigureAwaitChecker
                await throttler.WaitAsync();
#pragma warning restore CAC001 // ConfigureAwaitChecker
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
#pragma warning disable CAC001 // ConfigureAwaitChecker
                            await body(element);
#pragma warning restore CAC001 // ConfigureAwaitChecker
                        }
                    finally
                    {
                        throttler.Release();
                    }
                }));
            }
#pragma warning disable CAC001 // ConfigureAwaitChecker
            await Task.WhenAll(tasks);
#pragma warning restore CAC001 // ConfigureAwaitChecker
        }
        return sw.Elapsed;
    }

    [Obsolete("Use Parallel.ForEachAsync instead")]
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