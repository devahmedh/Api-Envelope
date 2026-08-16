using Microsoft.AspNetCore.Http;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Decides whether a request is exempt from enveloping.</summary>
internal static class EnvelopeBypass
{
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

        return context.GetEndpoint()?.Metadata.GetMetadata<NoEnvelopeAttribute>() is not null;
    }
}
