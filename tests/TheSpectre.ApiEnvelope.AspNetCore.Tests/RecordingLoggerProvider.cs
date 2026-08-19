using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

/// <summary>One captured log entry.</summary>
public sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

/// <summary>Captures every log entry the host writes, for assertion.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        // Every level is enabled here so a test can assert that an entry was NOT written
        // because of the writer's own routing, never because the provider filtered it out.
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
}
