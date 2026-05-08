#if NET8_0_OR_GREATER
namespace CasCap.Common.Abstractions;

/// <summary>Represents a single HTTP request/response audit record.</summary>
public record HttpAuditEntry
{
    /// <summary>Auto-generated primary key.</summary>
    public long Id { get; set; }

    /// <summary>UTC timestamp when the request was initiated.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>Logical source name (e.g. the typed HttpClient name).</summary>
    public required string Source { get; init; }

    /// <summary>HTTP method (GET, POST, PUT, DELETE, etc.).</summary>
    public required string HttpMethod { get; init; }

    /// <summary>Full request URI including query string.</summary>
    public required string RequestUri { get; init; }

    /// <summary>HTTP response status code.</summary>
    public int StatusCode { get; init; }

    /// <summary>Round-trip elapsed time in milliseconds.</summary>
    public double ElapsedMs { get; init; }

    /// <summary>Raw JSON request body, or <see langword="null"/> for bodyless requests.</summary>
    public string? RequestBody { get; init; }

    /// <summary>Raw JSON response body.</summary>
    public string? ResponseBody { get; init; }
}
#endif
