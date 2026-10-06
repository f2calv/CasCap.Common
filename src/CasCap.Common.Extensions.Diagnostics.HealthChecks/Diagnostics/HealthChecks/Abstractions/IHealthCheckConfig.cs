namespace CasCap.Common.Diagnostics.HealthChecks.Abstractions;

/// <summary>Defines health check configuration properties shared by all HTTP endpoint health checks.</summary>
public interface IHealthCheckConfig
{
    /// <summary>The health check endpoint URI to probe.</summary>
    public string HealthCheckUri { get; }

    /// <summary>The HTTP status codes considered healthy. Defaults to <c>[200]</c>.</summary>
#if NETSTANDARD2_0
    public IReadOnlyList<int> HealthCheckExpectedHttpStatusCodes { get; }
#else
    public IReadOnlyList<int> HealthCheckExpectedHttpStatusCodes => [200];
#endif
}
