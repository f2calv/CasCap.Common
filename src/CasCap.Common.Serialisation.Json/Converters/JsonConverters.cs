using CasCap.Common.Extensions;
using Newtonsoft.Json;
using System;
using System.Reflection;
namespace CasCap.Models
{
    //https://stackoverflow.com/questions/10302902/can-you-tell-json-net-to-serialize-datetime-as-utc-even-if-unspecified/10305908
    /*
    public class UtcJsonDateTimeConverter : JsonConverter
    {
        const string DefaultDateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFZ";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            bool nullable = objectType == typeof(DateTime?);
            if (reader.TokenType == JsonToken.Null)
            {
                if (!nullable) throw new JsonSerializationException($"Cannot convert null value to {objectType}.");
                return null;
            }

            if (reader.TokenType == JsonToken.Date)
                return reader.Value;
            else if (reader.TokenType != JsonToken.String)
                throw new JsonSerializationException($"Unexpected token parsing date. Expected String, got {reader.TokenType}.");

            string date_text = reader.Value.ToString();
            if (string.IsNullOrEmpty(date_text) && nullable)
                return null;
            
            return DateTime.Parse(date_text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is DateTime dateTime)
            {
                var str = dateTime.ToString(DefaultDateTimeFormat, CultureInfo.InvariantCulture);
                writer.WriteValue(str);
            }
            else
                throw new JsonSerializationException(
                    $"Unexpected value when converting date. Expected DateTime or DateTimeOffset, got {value.GetType()}.");
        }
    }
    */

    //https://stackoverflow.com/questions/34185295/handling-null-objects-in-custom-jsonconverters-readjson-method
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

    public class ColumnCleaner : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(DateTime);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            //if (reader.Value is null) return null;
            var sDate = (string?)reader.Value;
            if (sDate == "N/A") return null;
            if (!DateTime.TryParse(sDate, out var time))
                throw new Exception($"{MethodBase.GetCurrentMethod().Name} unable to parse '{nameof(time)}'??");
            return time;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            => throw new NotImplementedException();
    }
    //todo: Uri convertor

    //public class MicrosecondEpochConverter : DateTimeConverterBase
    //{
    //    static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    //    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    //    {
    //        if (reader.Value is null) { return null; }
    //        var t = long.Parse((string)reader.Value);
    //        return _epoch.AddMilliseconds(t / 1000d);
    //    }

    //    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    //    {
    //        writer.WriteRawValue(((DateTime)value - _epoch).TotalMilliseconds + "000");
    //    }
    //}
}