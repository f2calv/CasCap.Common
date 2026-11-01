using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

public sealed class RuntimeConfigurationFailureLog : ILoggerProvider, ILogger
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public TaskCompletionSource<bool> Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ILogger CreateLogger(string categoryName) => this;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
    public void Dispose() { }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (message.Contains("refresh failed", StringComparison.Ordinal))
        {
            Messages.Enqueue(message);
            Failed.TrySetResult(true);
        }
    }
}
