using System;
using System.Runtime.CompilerServices;
namespace Microsoft.Extensions.Logging;

public static class LoggingHelpers
{
    public static void LogError(this ILogger logger, Exception ex, [CallerMemberName] string caller = "")
    {
        logger.LogError(ex, "{caller} - {ex.Message}", ex.GetType().ToString());//3rd parameter to prevent infinite loop
    }
}