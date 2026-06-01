using Microsoft.Extensions.Logging;

namespace Dekori.Tests.Support;

/// <summary>An <see cref="ILoggerProvider"/> that records every log entry for assertions.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly List<LogEntry> _entries = new();
    private readonly Lock _gate = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToList();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, this);

    public void Dispose()
    {
    }

    private void Add(LogEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    private sealed class RecordingLogger(string category, RecordingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            owner.Add(new LogEntry(logLevel, category, formatter(state, exception), exception));
        }
    }
}
