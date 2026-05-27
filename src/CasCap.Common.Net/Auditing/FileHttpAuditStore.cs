#if NET8_0_OR_GREATER
using System.IO;
using System.Text.Json;
using CasCap.Common.Converters;

namespace CasCap.Common.Auditing;

/// <summary>
/// Lightweight file-based <see cref="IHttpAuditStore"/> that writes each audit entry as an individual
/// JSON file, organised into daily sub-folders (<c>yyyy-MM-dd</c>).
/// </summary>
/// <remarks>
/// Intended as a zero-dependency fallback for consumers who do not have PostgreSQL (or another
/// persistent store) available. Files are written to <paramref name="outputDirectory"/> which
/// defaults to <c>./http-audit</c> relative to the working directory.
/// JSON request/response bodies are embedded as raw JSON objects (not escaped strings) so they
/// are directly readable and extractable from the output files.
/// </remarks>
public sealed class FileHttpAuditStore(
    ILogger<FileHttpAuditStore> logger,
    string outputDirectory = "http-audit"
) : IHttpAuditStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new RawJsonStringConverter() },
    };

    /// <inheritdoc/>
    public async ValueTask SaveAsync(HttpAuditEntry entry, CancellationToken cancellationToken = default)
    {
        var datePart = entry.TimestampUtc.ToString("yyyy-MM-dd");
        var dayDir = outputDirectory.Extend(datePart).EnsureDirectoryExists();

        var timestamp = entry.TimestampUtc.ToString("HHmmss-fff");
        var fileName = $"{timestamp}_{entry.Source}_{entry.StatusCode}.json";
        var filePath = dayDir.Extend(fileName);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, entry, s_jsonOptions, cancellationToken).ConfigureAwait(false);

        logger.LogTrace("{ClassName} wrote audit entry to {FilePath}", nameof(FileHttpAuditStore), filePath);
    }
}
#endif
