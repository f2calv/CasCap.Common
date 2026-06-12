using CasCap.Common.Converters;
using System.Text.Json.Serialization;

namespace CasCap.Common.Serialization.Tests;

/// <summary>Tests for the custom <see cref="JsonConverter"/> implementations.</summary>
public class JsonConverterTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    /// <summary>Round-trips a 2-D array through <see cref="Array2DConverter"/> preserving values and shape.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void Array2DConverter_RoundTripsRectangularArray()
    {
        //Arrange
        var options = new JsonSerializerOptions { Converters = { new Array2DConverter() } };
        var original = new[,] { { 1, 2, 3 }, { 4, 5, 6 } };

        //Act
        var json = JsonSerializer.Serialize(original, options);
        var roundTripped = JsonSerializer.Deserialize<int[,]>(json, options);

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
        var options = new JsonSerializerOptions { Converters = { new MicrosecondEpochConverter() } };
        // 2020-01-01T00:00:00Z in microseconds since the Unix epoch.
        const long micros = 1_577_836_800_000_000;
        var json = micros.ToString();

        //Act
        var dt = JsonSerializer.Deserialize<DateTime?>(json, options);
        var written = JsonSerializer.Serialize(dt, options);

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
        var options = new JsonSerializerOptions { Converters = { new MicrosecondEpochConverter() } };

        //Act
        var dt = JsonSerializer.Deserialize<DateTime?>("null", options);

        //Assert
        Assert.Null(dt);
    }

    /// <summary>Verifies <see cref="MillisecondEpochConverter"/> reads a millisecond epoch string and writes it back.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void MillisecondEpochConverter_RoundTripsUtcDateTime()
    {
        //Arrange
        var options = new JsonSerializerOptions { Converters = { new MillisecondEpochConverter() } };
        // 2020-01-01T00:00:00Z in milliseconds since the Unix epoch.
        const long millis = 1_577_836_800_000;
        var json = millis.ToString();

        //Act
        var dt = JsonSerializer.Deserialize<DateTime?>(json, options);
        var written = JsonSerializer.Serialize(dt, options);

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
        var options = new JsonSerializerOptions { Converters = { new ParseEnumConverter<DayOfWeek>() } };

        //Act
        var value = JsonSerializer.Deserialize<DayOfWeek>(json, options);
        var written = JsonSerializer.Serialize(value, options);

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
        var options = new JsonSerializerOptions { Converters = { new RawJsonStringConverter() } };
        const string nested = "{\"a\":1,\"b\":[2,3]}";

        //Act
        var written = JsonSerializer.Serialize(nested, options);

        //Assert — no escaping; embedded as a nested object.
        Assert.Equal(nested, written);
    }

    /// <summary>Verifies <see cref="RawJsonStringConverter"/> writes a non-JSON string as a plain escaped value.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void RawJsonStringConverter_WritesNonJsonAsString()
    {
        //Arrange
        var options = new JsonSerializerOptions { Converters = { new RawJsonStringConverter() } };

        //Act
        var written = JsonSerializer.Serialize("hello world", options);

        //Assert
        Assert.Equal("\"hello world\"", written);
    }

    /// <summary>Verifies <see cref="RawJsonStringConverter"/> writes a null value as a JSON null token.</summary>
    [Fact]
    [Trait("Category", "Serialization")]
    public void RawJsonStringConverter_WritesNull()
    {
        //Arrange
        var options = new JsonSerializerOptions { Converters = { new RawJsonStringConverter() } };

        //Act
        var written = JsonSerializer.Serialize((string?)null, options);

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
        var options = new JsonSerializerOptions { Converters = { new StringToIntConverter() } };

        //Act
        var value = JsonSerializer.Deserialize<int?>(json, options);
        var written = JsonSerializer.Serialize(value, options);

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
        var options = new JsonSerializerOptions { Converters = { new StringToIntConverter() } };

        //Act
        var value = JsonSerializer.Deserialize<int?>(json, options);

        //Assert
        Assert.Null(value);
    }
}
