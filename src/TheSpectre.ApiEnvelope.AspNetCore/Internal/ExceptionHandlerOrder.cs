using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>
/// Records which <see cref="IExceptionHandler"/> implementations were already registered when
/// <c>AddApiEnvelope()</c> ran, so the application can be warned that they will run first.
/// </summary>
/// <remarks>
/// The exception-handler middleware walks its handlers in registration order and stops at the
/// first one returning <see langword="true"/>. A catch-all handler registered before
/// <c>AddApiEnvelope()</c> therefore swallows every exception this library would have
/// enveloped — including every <see cref="AppException"/> carrying its own status code, which
/// collapses 401, 404, 409 and 422 alike into whatever the other handler writes. The package
/// looks correctly installed and configured throughout, and only a hand-checked status code
/// reveals it. Migrating applications almost always have such a handler already, which is what
/// makes this worth a warning rather than a documentation note alone.
/// </remarks>
internal sealed class ExceptionHandlerOrder
{
    private const string LoggerCategory = "TheSpectre.ApiEnvelope.AspNetCore";

    private ExceptionHandlerOrder(IReadOnlyList<string> precedingHandlers) =>
        PrecedingHandlers = precedingHandlers;

    /// <summary>Handler type names registered ahead of this library's, in registration order.</summary>
    internal IReadOnlyList<string> PrecedingHandlers { get; }

    /// <summary>
    /// Captures the current registrations. Must be called <b>before</b> this library registers
    /// its own handler, or that handler counts itself as preceding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    internal static void Capture(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Read from the IServiceCollection rather than resolving IEnumerable<IExceptionHandler>
        // later: resolving would construct every handler in the application at pipeline-build
        // time purely to inspect it, and the descriptors already answer the question exactly.
        services.AddSingleton(new ExceptionHandlerOrder(Describe(services)));
    }

    /// <summary>Names the handlers registered ahead of this library's, in registration order.</summary>
    /// <param name="services">The service collection.</param>
    internal static IReadOnlyList<string> Describe(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var names = new List<string>();

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(IExceptionHandler))
            {
                continue;
            }

            var type = descriptor.ImplementationType
                ?? descriptor.ImplementationInstance?.GetType();

            // Skip our own: a second AddApiEnvelope() call would otherwise see the handler the
            // first call registered and report the library as preceding itself.
            if (type == typeof(ApiEnvelopeExceptionHandler))
            {
                continue;
            }

            names.Add(type?.Name ?? "a handler registered by factory");
        }

        return names;
    }

    /// <summary>Logs one warning if anything was captured.</summary>
    /// <param name="services">The application's service provider.</param>
    internal static void WarnIfPreceded(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var captured = services.GetService<ExceptionHandlerOrder>();

        if (captured is null || captured.PrecedingHandlers.Count == 0)
        {
            return;
        }

        services.GetService<ILoggerFactory>()?.CreateLogger(LoggerCategory).LogWarning(
            "{Handlers} is registered as an IExceptionHandler before AddApiEnvelope(), so it " +
            "runs first and any exception it handles never reaches the envelope — AppException " +
            "included, which means its status code is lost too. Either call AddApiEnvelope() " +
            "before AddExceptionHandler(...), or return false from that handler so it logs " +
            "without writing the response.",
            string.Join(", ", captured.PrecedingHandlers));
    }
}
