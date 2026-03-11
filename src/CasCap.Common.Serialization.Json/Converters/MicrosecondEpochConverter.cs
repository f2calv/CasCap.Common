namespace CasCap.Common.Converters;

/// <summary>
/// <see cref="System.Text.Json.Serialization.JsonConverter{T}"/> that converts a microsecond Unix epoch
/// value (as a string token) to and from a nullable <see cref="DateTime"/>.
/// </summary>
public class MicrosecondEpochConverter : JsonConverter<DateTime?>
{
    private static readonly DateTime _epoch =
#if NET8_0_OR_GREATER
        DateTime.UnixEpoch;
#else
        new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
#endif

    /// <inheritdoc/>
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return long.TryParse(reader.GetString(), out var t) ? _epoch.AddMilliseconds(t / 1000d) : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTime? dateTimeValue, JsonSerializerOptions options)
    {
        if (dateTimeValue.HasValue)
            writer.WriteRawValue((dateTimeValue.Value - _epoch).TotalMilliseconds + "000");
    }
}
