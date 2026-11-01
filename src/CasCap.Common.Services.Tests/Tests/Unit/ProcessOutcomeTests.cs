using System.Diagnostics;

namespace CasCap.Common.Services.Tests;

public sealed class ProcessOutcomeTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    [Theory, Trait("Category", "Hosting")]
    [InlineData("daemon-failure", 1)]
    [InlineData("graceful-shutdown", 0)]
    [InlineData("migration-success", 0)]
    [InlineData("migration-failure", 1)]
    public async Task ProcessExitCode(string mode, int expected)
    {
        var fixture = FixtureAssemblyPath();
        var startInfo = new ProcessStartInfo(DotnetHostPath())
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(fixture);
        startInfo.ArgumentList.Add(mode);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the process fixture.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Process fixture mode '{mode}' exceeded {_timeout}.");
        }

        var output = await standardOutput;
        var error = await standardError;
        Assert.True(
            process.ExitCode == expected,
            $"Mode '{mode}' returned {process.ExitCode}, expected {expected}.{Environment.NewLine}"
            + $"stdout:{Environment.NewLine}{output}{Environment.NewLine}"
            + $"stderr:{Environment.NewLine}{error}");
    }

    /// <summary>Resolves the absolute dotnet host path so the fixture never runs whatever <c>PATH</c> resolves first.</summary>
    private static string DotnetHostPath()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host))
            return host;
        var executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, executable);
            if (Path.IsPathFullyQualified(candidate) && File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException("Could not locate the dotnet host.");
    }

    private static string FixtureAssemblyPath()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CasCap.Common.slnx")))
            root = root.Parent;
        if (root is null)
            throw new DirectoryNotFoundException("Could not locate the CasCap.Common repository root.");

#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var framework = Path.GetFileName(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.Combine(
            root.FullName,
            "src",
            "CasCap.Common.Services.TestHost",
            "bin",
            configuration,
            framework,
            "CasCap.Common.Services.TestHost.dll");
    }
}
