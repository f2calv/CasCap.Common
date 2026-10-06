using CasCap.Common.Converters;
using System.Text.Json.Serialization;

namespace CasCap.Common.Serialization.Tests;

/// <summary>Tests for the custom <see cref="JsonConverter"/> implementations.</summary>
public class JsonConverterTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    private static readonly JsonSerializerOptions s_array2DOptions = new() { Converters = { new Array2DConverter() } };
    private static readonly JsonSerializerOptions s_microsecondEpochOptions = new() { Converters = { new MicrosecondEpochConverter() } };
    private static readonly JsonSerializerOptions s_millisecondEpochOptions = new() { Converters = { new MillisecondEpochConverter() } };
    private static readonly JsonSerializerOptions s_parseEnumOptions = new() { Converters = { new ParseEnumConverter<DayOfWeek>() } };
    private static readonly JsonSerializerOptions s_rawJsonOptions = new() { Converters = { new RawJsonStringConverter() } };
    private static readonly JsonSerializerOptions s_stringToIntOptions = new() { Converters = { new StringToIntConverter() } };

    /// <summary>Round-trips a 2-D array through <see cref="Array2DConverter"/> preserving values and shape.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void Array2DConverter_RoundTripsRectangularArray()
    {
        //Arrange
        var original = new[,] { { 1, 2, 3 }, { 4, 5, 6 } };

        //Act
        var json = JsonSerializer.Serialize(original, s_array2DOptions);
        var roundTripped = JsonSerializer.Deserialize<int[,]>(json, s_array2DOptions);

        //Assert
        Assert.Equal("[[1,2,3],[4,5,6]]", json);
        Assert.NotNull(roundTripped);
        Assert.Equal(2, roundTripped!.GetLength(0));
        Assert.Equal(3, roundTripped.GetLength(1));
        Assert.Equal(original, roundTripped);
    }

    /// <summary>Verifies <see cref="MicrosecondEpochConverter"/> reads a microsecond epoch string and writes it back.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void MicrosecondEpochConverter_RoundTripsUtcDateTime()
    {
        //Arrange
        // 2020-01-01T00:00:00Z in microseconds since the Unix epoch.
        const long micros = 1_577_836_800_000_000;
        var json = micros.ToString();

        //Act
        var dt = JsonSerializer.Deserialize<DateTime?>(json, s_microsecondEpochOptions);
        var written = JsonSerializer.Serialize(dt, s_microsecondEpochOptions);

        //Assert
        Assert.NotNull(dt);
        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), dt!.Value.ToUniversalTime());
        Assert.Equal(json, written);
    }

    /// <summary>Verifies <see cref="MicrosecondEpochConverter"/> maps a null token to a null <see cref="DateTime"/>.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void MicrosecondEpochConverter_ReadsNull()
    {
        //Arrange
        //Act
        var dt = JsonSerializer.Deserialize<DateTime?>("null", s_microsecondEpochOptions);

        //Assert
        Assert.Null(dt);
    }

    /// <summary>Verifies <see cref="MillisecondEpochConverter"/> reads a millisecond epoch string and writes it back.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void MillisecondEpochConverter_RoundTripsUtcDateTime()
    {
        //Arrange
        // 2020-01-01T00:00:00Z in milliseconds since the Unix epoch.
        const long millis = 1_577_836_800_000;
        var json = millis.ToString();

        //Act
        var dt = JsonSerializer.Deserialize<DateTime?>(json, s_millisecondEpochOptions);
        var written = JsonSerializer.Serialize(dt, s_millisecondEpochOptions);

        //Assert
        Assert.NotNull(dt);
        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), dt!.Value.ToUniversalTime());
        Assert.Equal(json, written);
    }

    /// <summary>Verifies <see cref="ParseEnumConverter{TEnum}"/> reads enum names case-insensitively and writes the member name.</summary>
    [Theory]
    [InlineData("\"Monday\"", DayOfWeek.Monday)]
    [InlineData("\"friday\"", DayOfWeek.Friday)]
    [InlineData("\"SUNDAY\"", DayOfWeek.Sunday)]
    [Trait("Category", "Serialization")]
    public void ParseEnumConverter_ReadsCaseInsensitiveAndWritesName(string json, DayOfWeek expected)
    {
        //Arrange
        //Act
        var value = JsonSerializer.Deserialize<DayOfWeek>(json, s_parseEnumOptions);
        var written = JsonSerializer.Serialize(value, s_parseEnumOptions);

        //Assert
        Assert.Equal(expected, value);
        Assert.Equal($"\"{expected}\"", written);
    }

    /// <summary>Verifies <see cref="RawJsonStringConverter"/> embeds valid JSON strings as nested objects.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void RawJsonStringConverter_EmbedsValidJsonRaw()
    {
        //Arrange
        const string nested = "{\"a\":1,\"b\":[2,3]}";

        //Act
        var written = JsonSerializer.Serialize(nested, s_rawJsonOptions);

        //Assert — no escaping; embedded as a nested object.
        Assert.Equal(nested, written);
    }

    /// <summary>Verifies <see cref="RawJsonStringConverter"/> writes a non-JSON string as a plain escaped value.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void RawJsonStringConverter_WritesNonJsonAsString()
    {
        //Arrange
        //Act
        var written = JsonSerializer.Serialize("hello world", s_rawJsonOptions);

        //Assert
        Assert.Equal("\"hello world\"", written);
    }

    /// <summary>Verifies <see cref="RawJsonStringConverter"/> writes a null value as a JSON null token.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void RawJsonStringConverter_WritesNull()
    {
        //Arrange
        //Act
        var written = JsonSerializer.Serialize((string?)null, s_rawJsonOptions);

        //Assert
        Assert.Equal("null", written);
    }

    /// <summary>Verifies <see cref="StringToIntConverter"/> parses a numeric string token to an <see cref="int"/>.</summary>
    [Theory]
    [InlineData("\"42\"", 42)]
    [InlineData("\"-7\"", -7)]
    [InlineData("\"0\"", 0)]
    [Trait("Category", "Serialization")]
    public void StringToIntConverter_ParsesNumericString(string json, int expected)
    {
        //Arrange
        //Act
        var value = JsonSerializer.Deserialize<int?>(json, s_stringToIntOptions);
        var written = JsonSerializer.Serialize(value, s_stringToIntOptions);

        //Assert
        Assert.Equal(expected, value);
        Assert.Equal(expected.ToString(), written);
    }

    /// <summary>Verifies <see cref="StringToIntConverter"/> returns null for a null token or a non-numeric string.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("\"not-a-number\"")]
    [Trait("Category", "Serialization")]
    public void StringToIntConverter_ReturnsNullForInvalidInput(string json)
    {
        //Arrange
        //Act
        var value = JsonSerializer.Deserialize<int?>(json, s_stringToIntOptions);

        //Assert
        Assert.Null(value);
    }
}
