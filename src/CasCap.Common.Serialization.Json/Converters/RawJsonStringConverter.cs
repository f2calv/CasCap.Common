#if NET8_0_OR_GREATER
namespace CasCap.Common.Converters;

/// <summary>
/// <see cref="JsonConverter{T}"/> that writes <see cref="string"/> values as raw JSON when the
/// content is valid JSON, avoiding double-encoding of serialised payloads.
/// </summary>
/// <remarks>
/// Non-JSON strings are written as plain string values. Useful when a DTO carries a JSON body
/// as a <see cref="string"/> property and the serialised output should embed it as a nested
/// object/array rather than an escaped string.
/// </remarks>
public sealed class RawJsonStringConverter : JsonConverter<string?>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString();

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        // Attempt to parse as JSON; if valid, write it raw so the output is a nested object/array.
        try
        {
            using var doc = JsonDocument.Parse(value);
            doc.RootElement.WriteTo(writer);
        }
        catch (JsonException)
        {
            // Not valid JSON — write as a plain string.
            writer.WriteStringValue(value);
        }
    }
}
#endif
