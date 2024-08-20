//using System.Globalization;

//namespace CasCap.Models;

///// <summary>
///// https://stackoverflow.com/questions/10302902/can-you-tell-json-net-to-serialize-datetime-as-utc-even-if-unspecified/10305908
///// </summary>
//public class UtcJsonDateTimeConverter : JsonConverter
//{
//    const string DefaultDateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFZ";

//    public override bool CanConvert(Type objectType)
//    {
//        return objectType == typeof(DateTime);
//    }

//    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//    {
//        bool nullable = objectType == typeof(DateTime?);
//        if (reader.TokenType == JsonToken.Null)
//        {
//            if (!nullable) throw new JsonSerializationException($"Cannot convert null value to {objectType}.");
//            return null;
//        }

//        if (reader.TokenType == JsonToken.Date)
//            return reader.Value;
//        else if (reader.TokenType != JsonToken.String)
//            throw new JsonSerializationException($"Unexpected token parsing date. Expected String, got {reader.TokenType}.");

//        string date_text = reader.Value.ToString();
//        if (string.IsNullOrEmpty(date_text) && nullable)
//            return null;

//        return DateTime.Parse(date_text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
//    }

//    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//    {
//        if (value is DateTime dateTime)
//        {
//            var str = dateTime.ToString(DefaultDateTimeFormat, CultureInfo.InvariantCulture);
//            writer.WriteValue(str);
//        }
//        else
//            throw new JsonSerializationException(
//                $"Unexpected value when converting date. Expected DateTime or DateTimeOffset, got {value.GetType()}.");
//    }
//}
