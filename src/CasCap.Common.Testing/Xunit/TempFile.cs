using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CasCap.Common.Xunit;

/// <summary>Creates a uniquely named file under the temporary folder and deletes it on dispose.</summary>
[ExcludeFromCodeCoverage]
public sealed class TempFile : IDisposable
{
    private TempFile(string path) => Path = path;

    /// <summary>Gets the full path of the temporary file.</summary>
    public string Path { get; }

    /// <summary>Creates an empty temporary file with the given extension.</summary>
    /// <param name="extension">The file extension, including the leading period.</param>
    public static TempFile Create(string extension = ".tmp")
    {
        var path = NewPath(extension);
        File.Create(path).Dispose();
        return new TempFile(path);
    }

    /// <summary>Writes the supplied bytes to a new temporary file.</summary>
    /// <param name="bytes">The file contents.</param>
    /// <param name="cancellationToken">A token that can cancel the write.</param>
    /// <param name="extension">The file extension, including the leading period.</param>
    public static async Task<TempFile> CreateAsync(byte[] bytes, CancellationToken cancellationToken, string extension = ".tmp")
    {
        var path = NewPath(extension);
#if NET8_0_OR_GREATER
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
#else
        await Task.Run(() => File.WriteAllBytes(path, bytes), cancellationToken).ConfigureAwait(false);
#endif
        return new TempFile(path);
    }

    /// <summary>Writes the supplied text to a new temporary file.</summary>
    /// <param name="contents">The file contents.</param>
    /// <param name="cancellationToken">A token that can cancel the write.</param>
    /// <param name="extension">The file extension, including the leading period.</param>
    public static async Task<TempFile> CreateAsync(string contents, CancellationToken cancellationToken, string extension = ".tmp")
    {
        var path = NewPath(extension);
        await File.WriteAllTextAsync(path, contents, cancellationToken).ConfigureAwait(false);
        return new TempFile(path);
    }

    private static string NewPath(string extension)
        => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            //A leaked temporary file must never fail an otherwise passing test.
        }
    }
}
