namespace CasCap.Common.Extensions;

public static class JsonExtensions
{
    private static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(JsonExtensions));

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
            _logger.LogError(ex, "{ClassName} {methodName} failed", nameof(JsonExtensions), nameof(JsonSerializer.Serialize));
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
            _logger.LogError(ex, "{ClassName} {methodName} failed to deserialize {objectType}",
                nameof(JsonExtensions), nameof(JsonSerializer.Deserialize), typeof(T));
            throw;
        }
    }

    public static bool TryFromJson<T>(this string json, out T? output, JsonSerializerOptions? options = null)
    {
        json = json ?? throw new ArgumentNullException(paramName: nameof(json));
        output = default;
        try
        {
            output = JsonSerializer.Deserialize<T>(json, options);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} {methodName} failed to deserialize {objectType}",
                nameof(JsonExtensions), nameof(JsonSerializer.Deserialize), typeof(T));
        }
        return false;
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
            JsonSerializer.Serialize(writer, value, options);
    }
}
