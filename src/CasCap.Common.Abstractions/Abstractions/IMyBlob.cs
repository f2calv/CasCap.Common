namespace CasCap.Common.Abstractions;

/// <summary>
/// Represents a blob with associated metadata.
/// </summary>
public interface IMyBlob
{
    /// <summary>
    /// The raw bytes of the blob content.
    /// </summary>
    public byte[] bytes { get; init; }

    /// <summary>
    /// The UTC timestamp when the blob was created.
    /// </summary>
    public DateTime DateCreatedUtc { get; init; }

    /// <summary>
    /// The generated file name for the blob.
    /// </summary>
    public string BlobName { get; init; }

    /// <summary>
    /// The size of the blob in bytes.
    /// </summary>
    public int SizeInBytes { get; }

    /// <summary>
    /// Indicates whether the blob contains any data.
    /// </summary>
    public bool HasImage { get; }
}
