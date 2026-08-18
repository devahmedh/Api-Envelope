using Microsoft.AspNetCore.Builder;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore;

/// <summary>Pipeline registration for the API envelope.</summary>
public static class ApiEnvelopeApplicationBuilderExtensions
{
    /// <summary>
    /// Adds correlation id resolution, global exception handling and status-code enveloping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this <b>before</b> <c>UseAuthentication()</c>:
    /// </para>
    /// <code>
    /// app.UseApiEnvelope();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapControllers();
    /// </code>
    /// <para>
    /// Placed after <c>UseAuthentication()</c>, the 401 challenge short-circuits outside this
    /// middleware and those responses ship with an empty body. That failure is silent and
    /// appears only in production, because a developer holding a valid token never sees it.
    /// </para>
    /// <para>
    /// A second ordering constraint lives on the service side: register
    /// <c>AddApiEnvelope()</c> <b>before</b> any <c>AddExceptionHandler(...)</c> the application
    /// already has, because handlers run in registration order and the first one returning
    /// <see langword="true"/> wins. This method warns when it finds the opposite.
    /// </para>
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseApiEnvelope(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Runs here rather than in AddApiEnvelope() because service registration order is not
        // guaranteed: a consumer may call AddApiEnvelope() before AddControllers().AddJsonOptions
        // and the comparison would then read options nobody had configured yet. By the time the
        // pipeline is being built, every ConfigureServices callback has run.
        JsonOptionsDivergence.WarnIfDiverged(app.ApplicationServices);
        ExceptionHandlerOrder.WarnIfPreceded(app.ApplicationServices);

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            AllowStatusCode404Response = true,

            // ExceptionHandlerMiddlewareImpl's constructor throws at startup unless one of
            // ExceptionHandler, ExceptionHandlingPath or a registered IProblemDetailsService is
            // set — even though ApiEnvelopeExceptionHandler (registered via
            // AddExceptionHandler<T> in AddApiEnvelope) handles every exception it sees. This
            // fallback delegate is only reached when that handler declines: an already-started
            // response, or a bypassed path. In both cases the correct behaviour is to do
            // nothing, so this is a no-op rather than calling AddProblemDetails() in
            // AddApiEnvelope(). Two reasons, not "it would pull in a package" (it ships in the
            // shared framework, same as everything else here): RFC 7807's human-readable
            // title/detail fields are an explicit non-goal of this library — the whole point is
            // a stable errorCode, not prose — and AddProblemDetails() is a process-wide
            // registration, so calling it here would affect every IProblemDetailsService
            // consumer in the app, not just this one fallback path.
            ExceptionHandler = static _ => Task.CompletedTask,
        });
        app.UseMiddleware<StatusCodeEnvelopeMiddleware>();

        return app;
    }
}
