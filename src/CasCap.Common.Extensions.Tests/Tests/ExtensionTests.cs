namespace CasCap.Common.Extensions.Tests;

/// <summary>Tests for common extension methods.</summary>
public class ExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    /// <summary>Verifies that a <see cref="DateTime"/> value can be round-tripped via Unix time milliseconds.</summary>
    [Fact]
    public void UnixTimeMS()
    {
        //Arrange
        var dt = DateTime.UtcNow;

        //Act
        var unixMS = dt.ToUnixTimeMs();
        var utcNow = unixMS.FromUnixTimeMs();

        //Assert
        Assert.Equal(dt.ToString(), utcNow.ToString());
    }

    /// <summary>Verifies that decimal string values are correctly scaled to integers by <c>Decimal2Int</c>.</summary>
    [Fact, Trait("Category", "Parsing")]
    public void Decimal2Int()
    {
        Assert.Equal(10023, "10023".Decimal2Int(0));
        Assert.Equal(100230, "10023".Decimal2Int(1));
        Assert.Equal(1002300, "10023".Decimal2Int(2));
        Assert.Equal(10023000, "10023".Decimal2Int(3));

        Assert.Equal(10023, "10023.3".Decimal2Int(0));
        Assert.Equal(100233, "10023.3".Decimal2Int(1));
        Assert.Equal(1002330, "10023.3".Decimal2Int(2));
        Assert.Equal(10023300, "10023.3".Decimal2Int(3));

        Assert.Equal(10023, "10023.34".Decimal2Int(0));
        Assert.Equal(100233, "10023.34".Decimal2Int(1));
        Assert.Equal(1002334, "10023.34".Decimal2Int(2));
        Assert.Equal(10023340, "10023.34".Decimal2Int(3));
    }
}
