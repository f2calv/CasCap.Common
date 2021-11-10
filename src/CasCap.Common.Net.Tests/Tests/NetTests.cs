using Xunit;
using Xunit.Abstractions;
namespace CasCap.Common.Net.Tests;

public class NetTests : TestBase
{
    public NetTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Placeholder()
    {
        var a = 1 + 1;
        var b = 2;
        Assert.Equal(a, b);
    }
}