namespace CasCap.Common.Serialization.Tests;

public class JsonTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
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
        var json = "abc".ToJSON();
        var json1 = obja.ToJSON();
        var json2 = obja.ToJSON(Formatting.Indented);
        var json3 = obja.ToJSON(new JsonSerializerSettings { MaxDepth = 100 });
        var json4 = obja.ToJSON(Formatting.Indented, new JsonSerializerSettings { MaxDepth = 100 });
        var obja1 = json1.FromJSON<MyTestClass3>();
        var obja2 = json2.FromJSON<MyTestClass3>();
        var obja3 = json3.FromJSON<MyTestClass3>();
        var obja4 = json4.FromJSON<MyTestClass3>();

        //Assert
        Assert.ThrowsAny<Exception>(() => erroneousDates.FromJSON<List<DateTime>>());
        Assert.NotNull(json1);
        Assert.NotNull(json2);
        Assert.NotNull(json3);
        Assert.NotNull(json4);
        Assert.Equal(obja, obja1);
        Assert.Equal(obja, obja2);
        Assert.Equal(obja, obja3);
        Assert.Equal(obja, obja4);
    }

    public class MyTestClass3
    {
        public int ID { get; set; } = 1337;
        public DateTime utcNow { get; set; } = DateTime.UtcNow;

        public override bool Equals(object obj)
        {
            return obj is MyTestClass3 @class &&
                   ID == @class.ID &&
                   utcNow == @class.utcNow;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
