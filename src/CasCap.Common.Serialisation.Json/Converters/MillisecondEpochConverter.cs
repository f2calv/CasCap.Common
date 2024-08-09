namespace CasCap.Models;

/// <summary>
/// https://stackoverflow.com/questions/34185295/handling-null-objects-in-custom-jsonconverters-readjson-method
/// </summary>
public class MillisecondEpochConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(DateTime);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        //if (reader.Value is null) return null;
        var milliseconds = (long?)reader.Value;
        return milliseconds.HasValue ? milliseconds.Value.FromUnixTimeMS() : (object?)null;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var dt = value as DateTime?;
        if (dt.HasValue)
            writer.WriteValue(dt.Value.ToUnixTimeMS());
    }
}
