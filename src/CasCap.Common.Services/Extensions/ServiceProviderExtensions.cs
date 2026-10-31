namespace CasCap.Common.Extensions;

/// <summary>Extension methods for reading the application process outcome.</summary>
public static class ServiceProviderExtensions
{
    /// <summary>Gets the process exit code recorded by background supervision.</summary>
    public static int GetApplicationExitCode(this IServiceProvider services) =>
        services.GetRequiredService<ApplicationExitState>().ExitCode;
}
