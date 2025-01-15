namespace CasCap.Models;

public class MicrosecondEpochConverter : JsonConverter<DateTime?>
{
    private static readonly DateTime _epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return long.TryParse(reader.GetString(), out var t) ? _epoch.AddMilliseconds(t / 1000d) : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? dateTimeValue, JsonSerializerOptions options)
    {
        if (dateTimeValue.HasValue)
            writer.WriteRawValue((dateTimeValue.Value - _epoch).TotalMilliseconds + "000");
    }
}
