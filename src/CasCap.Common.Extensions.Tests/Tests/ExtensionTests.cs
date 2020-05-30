using System;
using Xunit;
namespace CasCap.Common.Extensions.Tests
{
    public class ExtensionTests// : TestBase
    {
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
}