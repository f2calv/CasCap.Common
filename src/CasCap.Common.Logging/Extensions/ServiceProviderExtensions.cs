namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Assign the registered ILoggerFactory service to the static LoggerFactory instance.
    /// </summary>
    public static void AddStaticLogging(this IServiceProvider serviceProvider)
    {
        ApplicationLogging.LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    }

    /*
    public static void Log2(this ILogger logger, LogLevel logLevel, string message,
        [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        var className = Path.GetFileNameWithoutExtension(sourceFilePath);
        logger.Log(logLevel, "{ClassName}.{MemberName}({LineNumber}): {Message}", className, memberName, sourceLineNumber, message);
    }

    public static void LogDbg(this ILogger logger, string message,
        [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        var className = Path.GetFileNameWithoutExtension(sourceFilePath);
        logger.LogDebug("{ClassName}.{MemberName}({LineNumber}): {Message}", className, memberName, sourceLineNumber, message);
    }

    public static void LogDbg(this ILogger logger, string? message,
        [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0,
        params object?[] args)
    {
        var className = Path.GetFileNameWithoutExtension(sourceFilePath);
        var newArgs = new object[args.Length + 3];
        args.CopyTo(newArgs, 3);
        newArgs[0] = className;
        newArgs[1] = memberName;
        newArgs[2] = sourceLineNumber;
        logger.LogDebug("{ClassName}.{MemberName}({LineNumber}): " + message, newArgs);
    }

    //public static void LogInfo(this ILogger logger, string message,
    //    [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    //{
    //    var className = Path.GetFileNameWithoutExtension(sourceFilePath);
    //    logger.LogInformation("{ClassName}.{MemberName}({LineNumber}): {Message}", className, memberName, sourceLineNumber, message);
    //}
    */
}
