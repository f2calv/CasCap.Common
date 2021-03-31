using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Xunit.Abstractions;
namespace CasCap.Common.Testing
{
    public class TestLogProvider : ILoggerProvider
    {
        readonly ITestOutputHelper _output;
        readonly ConcurrentDictionary<string, TestLogger> _loggers;

        public TestLogProvider(ITestOutputHelper output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _loggers = new ConcurrentDictionary<string, TestLogger>(StringComparer.OrdinalIgnoreCase);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, _ => new TestLogger(_output));
        }

        public void Dispose() => _loggers.Clear();
    }

    class TestLogger : ILogger
    {
        readonly ITestOutputHelper _output;
        readonly List<LogEntry> _entries;

        public TestLogger(ITestOutputHelper output)
        {
            _output = output;
            _entries = new List<LogEntry>();
        }

        public IReadOnlyCollection<LogEntry> GetLogs() => this._entries.AsReadOnly();

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var entry = new LogEntry(logLevel, formatter(state, exception));
            _entries.Add(entry);
            _output.WriteLine(entry.ToString());
        }
    }

    class LogEntry
    {
        public LogEntry(LogLevel level, string message)
        {
            this.LogLevel = level;
            this.Message = message;
            this.Timestamp = DateTime.Now;
        }

        public DateTime Timestamp { get; }

        public LogLevel LogLevel { get; }

        public string Message { get; }

        public override string ToString()
        {
            return $"{this.Timestamp:o}: {this.Message}";
        }
    }
}