namespace CasCap.Common.Extensions;

public static class JsonSerializationHelpers
{
    private static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(JsonSerializationHelpers));

    public static string ToJson(this object obj) => obj.ToJson(options: null);

    public static string ToJson(this object obj, JsonSerializerOptions? options)
    {
        obj = obj ?? throw new ArgumentNullException(paramName: nameof(obj));
        try
        {
            return JsonSerializer.Serialize(obj, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{className} {methodName} failed", nameof(JsonSerializationHelpers), nameof(JsonSerializer.Serialize));
            throw;
        }
    }

    public static T? FromJson<T>(this string json) => json.FromJson<T>(options: null);

    public static T? FromJson<T>(this string json, JsonSerializerOptions? options)
    {
        json = json ?? throw new ArgumentNullException(paramName: nameof(json));
        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{className} {methodName} failed", nameof(JsonSerializationHelpers), nameof(JsonSerializer.Deserialize));
            throw;
        }
    }

    public static T[,] To2D<T>(this List<List<T>> source)
    {
        // Adapted from this answer https://stackoverflow.com/a/26291720/3744182
        // By https://stackoverflow.com/users/3909293/diligent-key-presser
        // To https://stackoverflow.com/questions/26291609/converting-jagged-array-to-2d-array-c-sharp
        var firstDim = source.Count;
        var secondDim = source.Select(row => row.Count).FirstOrDefault();
        if (!source.All(row => row.Count == secondDim))
            throw new InvalidOperationException();
        var result = new T[firstDim, secondDim];
        for (var i = 0; i < firstDim; i++)
            for (int j = 0, count = source[i].Count; j < count; j++)
                result[i, j] = source[i][j];
        return result;
    }

    public static void WriteOrSerialize<T>(this JsonConverter<T> converter, Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (converter != null)
            converter.Write(writer, value, options);
        else
            JsonSerializer.Serialize(writer, value, typeof(T), options);
    }
}
