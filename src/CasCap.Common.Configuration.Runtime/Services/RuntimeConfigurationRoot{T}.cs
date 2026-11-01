using CasCap.Common.Configuration.Runtime.Providers;
using Microsoft.Extensions.Configuration;

namespace CasCap.Common.Configuration.Runtime.Services;

/// <summary>Disposes the private configuration root with its owning host.</summary>
internal sealed class RuntimeConfigurationRoot<T>(RuntimeSnapshotProvider provider) : IDisposable where T : class
{
    /// <summary>Gets the root that is bound only to the registered runtime DTO.</summary>
    internal ConfigurationRoot Root { get; } = new([provider]);

    /// <inheritdoc/>
    public void Dispose() => Root.Dispose();
}
