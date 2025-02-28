namespace CasCap.Common.Extensions.Tests;

public class ExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Fact]
    public void UnixTimeMS()
    {
        //Arrange
        var dt = DateTime.UtcNow;

        //Act
        var unixMS = dt.ToUnixTimeMS();
        var utcNow = unixMS.FromUnixTimeMS();

        //Assert
        Assert.Equal(dt.ToString(), utcNow.ToString());
    }

    [Fact, Trait("Category", "Parsing")]
    public void decimal2int()
    {
        Assert.Equal(10023, "10023".decimal2int(0));
        Assert.Equal(100230, "10023".decimal2int(1));
        Assert.Equal(1002300, "10023".decimal2int(2));
        Assert.Equal(10023000, "10023".decimal2int(3));

        Assert.Equal(10023, "10023.3".decimal2int(0));
        Assert.Equal(100233, "10023.3".decimal2int(1));
        Assert.Equal(1002330, "10023.3".decimal2int(2));
        Assert.Equal(10023300, "10023.3".decimal2int(3));

        Assert.Equal(10023, "10023.34".decimal2int(0));
        Assert.Equal(100233, "10023.34".decimal2int(1));
        Assert.Equal(1002334, "10023.34".decimal2int(2));
        Assert.Equal(10023340, "10023.34".decimal2int(3));
    }
}
