using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

/// <summary>
/// An <see cref="ILoggerProvider"/> whose logger throws from every <see cref="ILogger.Log"/>
/// call made under one of this library's own categories (see
/// <c>TheSpectre.ApiEnvelope.AspNetCore.Internal.LoggerCategories</c>), standing in for a
/// host-registered sink (e.g. a remote log collector) failing on a transient error. Used to
/// prove that a logging failure can never cost the caller their error envelope.
/// </summary>
/// <remarks>
/// Every other category — including the ASP.NET Core hosting categories used while the test
/// host itself starts up — gets a no-op logger, so the throw is isolated to exactly the call
/// <see cref="TheSpectre.ApiEnvelope.AspNetCore.Internal.EnvelopeResponseWriter"/> makes.
/// </remarks>
public sealed class ThrowingLoggerProvider : ILoggerProvider
{
    private const string LibraryCategoryPrefix = "TheSpectre.ApiEnvelope.";

    public ILogger CreateLogger(string categoryName) =>
        categoryName.StartsWith(LibraryCategoryPrefix, StringComparison.Ordinal)
            ? new ThrowingLogger()
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Simulated logging sink failure.");
    }
}
