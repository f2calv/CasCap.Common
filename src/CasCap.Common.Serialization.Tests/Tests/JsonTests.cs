namespace CasCap.Common.Serialization.Tests;

/// <summary>Tests for JSON serialization extension methods.</summary>
public class JsonTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    /// <summary>Verifies JSON serialization and deserialization of objects via extension methods.</summary>
    [Fact]
    public void TestExtensions()
    {
        //Arrange
        var obja = new MyTestClass3();
        //var objb = new MyTestClass3();
        var erroneousDates = @"[
          '2009-09-09T00:00:00Z',
          'I am not a date and will error!',
          [
            1
          ],
          '1977-02-20T00:00:00Z',
          null,
          '2000-12-01T00:00:00Z'
        ]";

        //Act
        var json = "abc".ToJson();
        var json1 = obja.ToJson();
        var json2 = obja.ToJson(new JsonSerializerOptions { WriteIndented = true });
        var json3 = obja.ToJson(new JsonSerializerOptions { MaxDepth = 100 });
        var json4 = obja.ToJson(new JsonSerializerOptions { MaxDepth = 100, WriteIndented = true });
        var obja1 = json1.FromJson<MyTestClass3>();
        var obja2 = json2.FromJson<MyTestClass3>();
        var obja3 = json3.FromJson<MyTestClass3>();
        var obja4 = json4.FromJson<MyTestClass3>();

        //Assert
        Assert.ThrowsAny<Exception>(() => erroneousDates.FromJson<List<DateTime>>());
        Assert.NotNull(json1);
        Assert.NotNull(json2);
        Assert.NotNull(json3);
        Assert.NotNull(json4);
        Assert.Equal(obja, obja1);
        Assert.Equal(obja, obja2);
        Assert.Equal(obja, obja3);
        Assert.Equal(obja, obja4);
    }

    /// <summary>A test model used to verify JSON serialization round-trips.</summary>
    public class MyTestClass3
    {
        /// <summary>An integer identifier.</summary>
        public int ID { get; set; } = 1337;

        /// <summary>A UTC timestamp.</summary>
        public DateTime utcNow { get; set; } = DateTime.UtcNow;

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is MyTestClass3 @class &&
                   ID == @class.ID &&
                   utcNow == @class.utcNow;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
