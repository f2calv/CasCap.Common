using System;
namespace CasCap.Common.Extensions
{
    public static class Helpers
    {
        public static TimeSpan? GetExpiry(this int ttl)
        {
            TimeSpan? expiry = null;
            if (ttl > -1)//if -1, the key does not have expiry timeout.
                expiry = TimeSpan.FromSeconds(ttl);
            return expiry;
        }
    }
}