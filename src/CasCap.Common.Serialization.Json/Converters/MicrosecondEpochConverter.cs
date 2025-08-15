namespace CasCap.Models;

public class MicrosecondEpochConverter : JsonConverter<DateTime?>
{
#if NETSTANDARD2_0
    private static readonly DateTime _epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
#endif

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
#if NET8_0_OR_GREATER
        return long.TryParse(reader.GetString(), out var t) ? DateTime.UnixEpoch.AddMilliseconds(t / 1000d) : null;
#else
        return long.TryParse(reader.GetString(), out var t) ? _epoch.AddMilliseconds(t / 1000d) : null;
#endif
    }

    public override void Write(Utf8JsonWriter writer, DateTime? dateTimeValue, JsonSerializerOptions options)
    {
        if (dateTimeValue.HasValue)
#if NET8_0_OR_GREATER
            writer.WriteRawValue((dateTimeValue.Value - DateTime.UnixEpoch).TotalMilliseconds + "000");
#else
            writer.WriteRawValue((dateTimeValue.Value - _epoch).TotalMilliseconds + "000");
#endif
    }
}
