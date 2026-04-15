using System.Net.WebSockets;

namespace CasCap.Common.Extensions;

/// <summary>Convenience helpers for <see cref="ClientWebSocket"/>.</summary>
public static class ClientWebSocketExtensions
{
    /// <summary>Sets the <c>Authorization</c> header to HTTP Basic on a <see cref="ClientWebSocket"/>.</summary>
    /// <remarks>WebSocket counterpart of <see cref="NetExtensions.SetBasicAuth(HttpClient, string, string)"/>.</remarks>
    /// <param name="ws">The <see cref="ClientWebSocket"/> to configure.</param>
    /// <param name="username">The Basic authentication username.</param>
    /// <param name="password">The Basic authentication password.</param>
    public static void SetBasicAuth(this ClientWebSocket ws, string username, string password)
    {
        var base64 = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{username}:{password}"));
        ws.Options.SetRequestHeader("Authorization", $"Basic {base64}");
    }
}
