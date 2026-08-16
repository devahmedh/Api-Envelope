using Microsoft.AspNetCore.Http;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Decides whether a request is exempt from enveloping.</summary>
internal static class EnvelopeBypass
{
    /// <summary>
    /// The <see cref="HttpContext.Items"/> key <see cref="StatusCodeEnvelopeMiddleware"/> stashes
    /// the matched <see cref="Microsoft.AspNetCore.Http.Endpoint"/> under.
    /// </summary>
    /// <remarks>
    /// When an endpoint throws, <c>ExceptionHandlerMiddlewareImpl.ClearHttpContext()</c> calls
    /// <c>HttpContext.SetEndpoint(null)</c> before any <c>IExceptionHandler</c> runs, so
    /// <c>context.GetEndpoint()</c> can no longer see <see cref="NoEnvelopeAttribute"/> by the
    /// time <c>ApiEnvelopeExceptionHandler</c> asks. This stashed copy is the fallback.
    /// </remarks>
    internal const string EndpointItemsKey = "TheSpectre.ApiEnvelope.Endpoint";

    /// <summary>
    /// Whether <paramref name="context"/> must not be enveloped, because its path is excluded
    /// or its endpoint carries <see cref="NoEnvelopeAttribute"/>.
    /// </summary>
    internal static bool ShouldBypass(HttpContext context, ApiEnvelopeOptions options)
    {
        foreach (var prefix in options.ExcludedPathPrefixes)
        {
            // StartsWithSegments, not StartsWith: "/health" must not match "/healthcheck".
            if (context.Request.Path.StartsWithSegments(prefix))
            {
                return true;
            }
        }

        // Prefer the live endpoint; fall back to the copy stashed before it could be cleared
        // by exception-handling middleware (see EndpointItemsKey).
        var endpoint = context.GetEndpoint()
            ?? context.Items[EndpointItemsKey] as Microsoft.AspNetCore.Http.Endpoint;

        return endpoint?.Metadata.GetMetadata<NoEnvelopeAttribute>() is not null;
    }
}
