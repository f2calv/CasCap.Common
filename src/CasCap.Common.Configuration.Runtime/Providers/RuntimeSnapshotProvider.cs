using Microsoft.Extensions.Configuration;
using System.Text;

namespace CasCap.Common.Configuration.Runtime.Providers;

/// <summary>Ports the prototype's atomic dictionary replacement into a prevalidated snapshot adapter.</summary>
internal sealed class RuntimeSnapshotProvider : ConfigurationProvider
{
    private string? _acceptedJson;

    /// <summary>Gets the most recently accepted complete JSON snapshot.</summary>
    internal string AcceptedJson => _acceptedJson
        ?? throw new InvalidOperationException("No runtime configuration snapshot has been accepted.");

    /// <summary>Publishes a complete effective change; unchanged polls do not raise reload tokens.</summary>
    internal void Publish(string json)
    {
        if (string.Equals(json, _acceptedJson, StringComparison.Ordinal))
            return;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var candidate = new ConfigurationBuilder().AddJsonStream(stream).Build();
        using var lifetime = (IDisposable)candidate;
        Data = candidate.AsEnumerable().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        _acceptedJson = json;
        OnReload();
    }
}
