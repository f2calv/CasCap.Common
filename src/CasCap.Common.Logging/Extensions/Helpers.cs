using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
namespace CasCap.Common.Extensions
{
    public static class Helpers
    {
        public static void LogError(this ILogger logger, Exception ex, [CallerMemberName] string caller = "")
        {
            logger.LogError(ex, $"{caller} - {ex.Message}", ex.GetType().ToString());//3rd parameter to prevent infinite loop
        }

        // public static ILoggerProvider AsLoggerProvider(this ILogger logger) => new ExistingLoggerProvider(logger);

        // class ExistingLoggerProvider : ILoggerProvider
        // {
        //     readonly ILogger _logger;

        //     public ExistingLoggerProvider(ILogger logger) => _logger = logger;

        //     public ILogger CreateLogger(string categoryName) => _logger;

        //     public void Dispose()
        //     {
        //         return;
        //     }
        // }
    }
}