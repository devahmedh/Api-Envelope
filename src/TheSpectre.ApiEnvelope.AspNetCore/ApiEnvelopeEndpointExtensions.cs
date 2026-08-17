using Microsoft.AspNetCore.Builder;
// EndpointFilterExtensions.AddEndpointFilter<TBuilder, TFilterType> lives in
// Microsoft.AspNetCore.Http, not .Builder — without this using, the generic overload is
// invisible and only the RouteHandlerBuilder/RouteGroupBuilder-specific ones resolve.
using Microsoft.AspNetCore.Http;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore;

/// <summary>Envelope opt-in and opt-out for minimal-API endpoints.</summary>
/// <remarks>
/// MVC controllers are covered globally by <c>AddApiEnvelope()</c>, because MVC filters can be
/// registered application-wide. Minimal APIs have no equivalent global hook, so they opt in
/// once at the root:
/// <code>
/// var api = app.MapGroup("").WithApiEnvelope();
/// api.MapGet("/projects", …);
/// </code>
/// </remarks>
public static class ApiEnvelopeEndpointExtensions
{
    /// <summary>Envelopes the responses of the endpoints this builder covers.</summary>
    /// <typeparam name="TBuilder">The convention builder type.</typeparam>
    /// <param name="builder">The endpoint or group to cover.</param>
    /// <remarks>
    /// Not every result passes through this: a handful of result kinds are deliberately left
    /// exactly as they are, both at runtime and in the response metadata this rewrites.
    /// <list type="bullet">
    /// <item><description>
    /// File results — the response body is the file's bytes, not JSON, so there is nothing to
    /// wrap.
    /// </description></item>
    /// <item><description>
    /// Redirects — the payload that matters is the <c>Location</c> header, and the body is
    /// typically empty or irrelevant.
    /// </description></item>
    /// <item><description>
    /// Challenges (401/403 authentication and authorization results) — these carry no JSON
    /// payload for the envelope to wrap either.
    /// </description></item>
    /// <item><description>
    /// <c>Content(...)</c> / <c>Results.Text(...)</c> — the consumer chose a raw content type on
    /// purpose, and enveloping would silently override that choice.
    /// </description></item>
    /// </list>
    /// This is a documented boundary, not an oversight: an endpoint returning one of these is
    /// working as designed, not falling through a gap.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static TBuilder WithApiEnvelope<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddEndpointFilter<TBuilder, ApiEnvelopeEndpointFilter>();
        builder.Finally(endpointBuilder => EnvelopeResponseMetadata.Rewrite(endpointBuilder));

        return builder;
    }

    /// <summary>Excludes the endpoints this builder covers from enveloping.</summary>
    /// <typeparam name="TBuilder">The convention builder type.</typeparam>
    /// <param name="builder">The endpoint or group to exclude.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static TBuilder WithoutApiEnvelope<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithMetadata(new NoEnvelopeAttribute());

        return builder;
    }
}
