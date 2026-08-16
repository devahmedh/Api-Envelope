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
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseApiEnvelope(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

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
            // nothing, so this is a no-op rather than pulling in
            // Microsoft.Extensions.Diagnostics.ProblemDetails via AddProblemDetails().
            ExceptionHandler = static _ => Task.CompletedTask,
        });
        app.UseMiddleware<StatusCodeEnvelopeMiddleware>();

        return app;
    }
}
