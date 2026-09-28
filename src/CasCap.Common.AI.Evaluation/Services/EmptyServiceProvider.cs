namespace CasCap.Common.Services;

/// <summary>A service provider that resolves nothing, for building tools whose services are never invoked.</summary>
public sealed class EmptyServiceProvider : IServiceProvider
{
    /// <summary>The shared instance.</summary>
    public static EmptyServiceProvider Instance { get; } = new();

    /// <inheritdoc/>
    public object? GetService(Type serviceType) => null;
}
