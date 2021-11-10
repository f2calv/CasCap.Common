using System;
using Xunit;
using Xunit.Abstractions;
namespace CasCap.Common.Extensions.Tests;

public class ExtensionTests : TestBase
{
    public ExtensionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void UnixTimeMS()
    {
        var dt = DateTime.UtcNow;

        var unixMS = dt.ToUnixTimeMS();

        var utcNow = unixMS.FromUnixTimeMS();
        //another Equals() issue...
        Assert.True(dt.ToString() == utcNow.ToString());
    }
}