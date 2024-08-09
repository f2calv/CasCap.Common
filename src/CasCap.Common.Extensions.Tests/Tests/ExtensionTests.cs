namespace CasCap.Common.Extensions.Tests;

public class ExtensionTests : TestBase
{
    public ExtensionTests(ITestOutputHelper output) : base(output) { }

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
}
