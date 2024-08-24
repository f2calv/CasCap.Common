namespace CasCap.Common.Net.Tests;

public class NetTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Fact]
    public void Placeholder()
    {
        var a = 1 + 1;
        var b = 2;
        Assert.Equal(a, b);
    }
}
