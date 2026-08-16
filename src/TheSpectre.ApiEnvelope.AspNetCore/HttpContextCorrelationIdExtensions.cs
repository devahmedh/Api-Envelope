using Microsoft.AspNetCore.Http;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore;

/// <summary>Correlation id access for application code.</summary>
public static class HttpContextCorrelationIdExtensions
{
    /// <summary>
    /// Returns the correlation id for the current request, or an empty string when
    /// <c>UseApiEnvelope()</c> has not run.
    /// </summary>
    /// <remarks>
    /// Application code rarely needs this: the id is pushed into the <c>ILogger</c> scope, so
    /// it already appears on every log line written during the request. Use it when the id
    /// must be surfaced somewhere else, such as an outbound call header.
    /// </remarks>
    /// <param name="context">The current request context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static string GetCorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(CorrelationId.ItemsKey, out var value)
            && value is string correlationId
                ? correlationId
                : string.Empty;
    }
}
