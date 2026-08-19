using Microsoft.Extensions.Logging;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>
/// The three loggers <see cref="EnvelopeResponseWriter"/> writes error envelopes under, resolved
/// once and held for the lifetime of the application.
/// </summary>
/// <remarks>
/// Holding these is safe because they are not a snapshot: each is the very same object
/// <see cref="ILoggerFactory"/> keeps in its own dictionary. <see cref="ILoggerFactory"/> is
/// built on <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> of
/// <see cref="LoggerFilterOptions"/>, and when that configuration changes it recomputes the
/// filter rules of every logger already in its dictionary in place — including these three. A
/// level change in <c>appsettings.json</c> therefore still takes effect; caching the reference
/// only removes the locked dictionary lookup <see cref="ILoggerFactory.CreateLogger(string)"/>
/// performs on every call. Do not "fix" this back to per-call resolution.
/// </remarks>
internal sealed class EnvelopeLoggers
{
    /// <param name="factory">The factory this instance's loggers are resolved from.</param>
    internal EnvelopeLoggers(ILoggerFactory factory)
    {
        Validation = factory.CreateLogger(LoggerCategories.Validation);
        StatusCode = factory.CreateLogger(LoggerCategories.StatusCode);
        Exception = factory.CreateLogger(LoggerCategories.Exception);
    }

    /// <summary>The logger for validation failures.</summary>
    internal ILogger Validation { get; }

    /// <summary>The logger for bare status-code envelopes.</summary>
    internal ILogger StatusCode { get; }

    /// <summary>The logger for unhandled and domain exceptions.</summary>
    internal ILogger Exception { get; }
}
