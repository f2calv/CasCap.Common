using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

/// <summary>Opt-in black-box acceptance against actual published executable artifacts, not the test runner.</summary>
[Trait("Category", "Integration")]
public sealed class PublishedConfigurationTests(ITestOutputHelper output) : TestBase(output)
{
    [Theory(Explicit = true)]
    [InlineData("postgres", "jit")]
    [InlineData("postgres", "aot")]
    [InlineData("sqlite", "jit")]
    [InlineData("sqlite", "aot")]
    public async Task PublishedProcesses_PersistReloadValidateAndReopen(string backend, string mode)
    =>
        await WithDatabaseAsync(backend, async connection =>
        {
            await RunAsync(backend, "jit", "migrate", connection);
            await RunAsync(backend, mode, "exercise", connection);
            await RunAsync(backend, mode, "verify", connection);
        });

    [Theory(Explicit = true)]
    [InlineData("postgres", "dynamic-filter")]
    [InlineData("postgres", "dynamic-aggregate")]
    [InlineData("sqlite", "dynamic-filter")]
    [InlineData("sqlite", "dynamic-aggregate")]
    public async Task DynamicQueries_JitReturnsRows_AotRejectsUncompiledShape(string backend, string scenario)
    =>
        await WithDatabaseAsync(backend, async connection =>
        {
            await RunAsync(backend, "jit", "migrate", connection);
            await RunAsync(backend, "jit", "exercise", connection);
            Assert.Contains("JIT_QUERY_RESULT_VERIFIED", await RunAsync(backend, "jit", scenario, connection));
            Assert.Contains("AOT_QUERY_NOT_PRECOMPILED_VERIFIED", await RunAsync(backend, "aot", scenario, connection));
        });

    private async Task WithDatabaseAsync(string backend, Func<string, Task> operation)
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        try
        {
            var connection = backend == "postgres" ? _connectionString
                : new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "configuration.db"), Pooling = false }.ConnectionString;
            await operation(connection);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root))
                File.Delete(file);
            Directory.Delete(root);
        }
    }

    private async Task<string> RunAsync(string backend, string mode, string phase, string connection)
    {
        var artifacts = Environment.GetEnvironmentVariable("CONFIGURATION_PUBLISHED_DIRECTORY")
            ?? throw new InvalidOperationException("Run the configuration-aot-tests Compose profile to publish both executables.");
        var executable = Path.Combine(artifacts, mode, "CasCap.Common.Configuration.Aot.TestHost");
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(backend);
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(phase);
        startInfo.Environment["ConfigurationAot__Connection"] = connection;
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The published configuration process could not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
        var diagnostics = (await stdout) + (await stderr);
        Assert.True(process.ExitCode == 0,
            $"{backend}/{mode}/{phase}: exit {process.ExitCode}. {diagnostics.Replace(connection, "[connection]")}");
        _output.WriteLine($"{backend}/{mode}/{phase}: published process passed.");
        return diagnostics;
    }
}
