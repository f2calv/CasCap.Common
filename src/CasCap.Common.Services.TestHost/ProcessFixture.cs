namespace CasCap.Common.Services.TestHost;

internal static class ProcessFixture
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            return args.SingleOrDefault() switch
            {
                "daemon-failure" => await RunDaemonAsync<FaultingFeature>("Faulting"),
                "graceful-shutdown" => await RunDaemonAsync<GracefulFeature>("Graceful"),
                "migration-success" => await RunMigrationAsync(fail: false),
                "migration-failure" => await RunMigrationAsync(fail: true),
                _ => 1,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task<int> RunDaemonAsync<TFeature>(string featureName)
        where TFeature : class, IBgFeature
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IBgFeature, TFeature>();
        builder.Services.AddFeatureFlagService(
            new HashSet<string>([featureName], StringComparer.OrdinalIgnoreCase));
        using var host = builder.Build();
        var exitState = host.Services.GetApplicationExitState();

        await host.RunAsync();
        return exitState.ExitCode;
    }

    private static Task<int> RunMigrationAsync(bool fail) =>
        fail
            ? Task.FromException<int>(new InvalidOperationException("Synthetic migration failure."))
            : Task.FromResult(0);

    private sealed class FaultingFeature(IHostApplicationLifetime lifetime) : IBgFeature
    {
        public string FeatureName => "Faulting";

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, lifetime.ApplicationStarted)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw new InvalidOperationException("Synthetic background failure.");
        }
    }

    private sealed class GracefulFeature(IHostApplicationLifetime lifetime) : IBgFeature
    {
        public string FeatureName => "Graceful";

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, lifetime.ApplicationStarted)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            lifetime.StopApplication();
        }
    }
}
