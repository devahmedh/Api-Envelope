using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>
/// Rewrites response metadata so it describes the envelope the runtime filters apply, rather
/// than the bare type a handler or action declared.
/// </summary>
/// <remarks>
/// Every OpenAPI generator — Swashbuckle, <c>Microsoft.AspNetCore.OpenApi</c>, NSwag — reads
/// <see cref="IProducesResponseTypeMetadata"/> entries, minimal APIs from
/// <see cref="Endpoint.Metadata"/> and MVC from <c>ApiDescription.SupportedResponseTypes</c>.
/// Without this rewrite a generated client would expect the bare DTO the handler declared,
/// while the wire carries <see cref="ApiResponse{T}"/> instead — the same mismatch this
/// library exists to remove from the response body, left uncorrected in the contract that
/// describes it. The type-rewriting members below (<see cref="WrapSuccessType"/>,
/// <see cref="NormaliseStatusCode"/>, <see cref="IsSuccessStatusCode"/> and
/// <see cref="ErrorPayloadType"/>) are also called by MVC's equivalent convention, so a
/// minimal-API handler and a controller action returning the same type describe the same
/// metadata.
/// </remarks>
internal static class EnvelopeResponseMetadata
{
    /// <summary>The type every synthesised 400/500 entry carries: the payload is lost on error.</summary>
    internal static readonly Type ErrorPayloadType = typeof(ApiResponse<object>);

    /// <summary>Rewrites <paramref name="builder"/>'s response metadata to describe the envelope.</summary>
    /// <param name="builder">The endpoint builder being finalised.</param>
    /// <remarks>
    /// Must be registered as a <c>Finally</c> convention, not an <c>Add</c> one: at the point
    /// regular <c>Add</c> conventions run, the framework has not yet populated the handler's own
    /// inferred <see cref="IProducesResponseTypeMetadata"/>, and a per-endpoint
    /// <see cref="NoEnvelopeAttribute"/> added later in the same fluent chain (for example by
    /// <c>WithoutApiEnvelope()</c>) is not present yet either. <c>Finally</c> conventions run
    /// after both, so this always sees the endpoint's fully assembled metadata.
    /// </remarks>
    internal static void Rewrite(EndpointBuilder builder)
    {
        if (builder.Metadata.OfType<NoEnvelopeAttribute>().Any())
        {
            return;
        }

        var statusCodes = RewriteSuccessEntries(builder.Metadata);
        AddMissingErrorEntries(builder.Metadata, statusCodes);
    }

    /// <summary>
    /// Replaces every 2xx <see cref="IProducesResponseTypeMetadata"/> entry with one describing
    /// <c>ApiResponse&lt;T&gt;</c> at the envelope's normalised status code, and returns every
    /// status code seen (success or not) so callers can decide which error entries are missing.
    /// </summary>
    internal static HashSet<int> RewriteSuccessEntries(IList<object> metadata)
    {
        var statusCodes = new HashSet<int>();

        // ToList: metadata is mutated inside the loop, so it cannot be enumerated live.
        foreach (var item in metadata.OfType<IProducesResponseTypeMetadata>().ToList())
        {
            if (!IsSuccessStatusCode(item.StatusCode))
            {
                statusCodes.Add(item.StatusCode);
                continue;
            }

            metadata.Remove(item);

            var normalisedStatusCode = NormaliseStatusCode(item.StatusCode);
            metadata.Add(new ProducesResponseTypeMetadata(normalisedStatusCode, WrapSuccessType(item.Type)));
            statusCodes.Add(normalisedStatusCode);
        }

        return statusCodes;
    }

    /// <summary>Adds 400 and 500 <c>ApiResponse&lt;object&gt;</c> entries not already present.</summary>
    internal static void AddMissingErrorEntries(IList<object> metadata, ISet<int> statusCodes)
    {
        if (statusCodes.Add(StatusCodes.Status400BadRequest))
        {
            metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status400BadRequest, ErrorPayloadType));
        }

        if (statusCodes.Add(StatusCodes.Status500InternalServerError))
        {
            metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status500InternalServerError, ErrorPayloadType));
        }
    }

    /// <summary>Whether <paramref name="statusCode"/> is a 2xx success status.</summary>
    internal static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and < 300;

    /// <summary>Maps a success status code to the one the envelope carries — 204 becomes 200.</summary>
    internal static int NormaliseStatusCode(int statusCode) => SuccessEnvelope.NormaliseStatusCode(statusCode);

    /// <summary>Wraps <paramref name="original"/> as <c>ApiResponse&lt;T&gt;</c>, defaulting to <c>object</c>.</summary>
    /// <remarks>
    /// <see cref="Type.MakeGenericType(Type[])"/> is the first reflection this library performs.
    /// It runs once per endpoint at startup while conventions are applied, never per request, so
    /// the cost is not the concern here; AOT-visibility is. <see cref="RuntimeFeature.IsDynamicCodeSupported"/>
    /// is a compile-time constant under Native AOT, so the guarded branch below never ships in an
    /// AOT binary: an AOT-published host loses the payload type in the reported schema instead,
    /// while the runtime envelope still carries the real payload. OpenAPI generators are
    /// themselves reflection-heavy enough that an AOT host is not the one generating documents
    /// anyway, so schema fidelity is kept exactly where it is achievable.
    /// <para>
    /// <see langword="typeof(void)"/> is treated the same as a missing type: the framework
    /// reports it for a handler with no return value (for example <c>TypedResults.NoContent()</c>),
    /// and <c>void</c> cannot itself be used as a generic type argument.
    /// </para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "The MakeGenericType call is guarded by RuntimeFeature.IsDynamicCodeSupported " +
            "and therefore cannot execute under Native AOT, where the guard is substituted " +
            "with false and the branch removed. The suppression is needed only because the " +
            "net8.0 reference assemblies lack the FeatureGuard annotation that lets the " +
            "net10.0 analyser prove this; it can be dropped when net8.0 support is.")]
    internal static Type WrapSuccessType(Type? original)
    {
        var payload = original is null || original == typeof(void) ? typeof(object) : original;

        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            return typeof(ApiResponse<>).MakeGenericType(payload);
        }

        return typeof(ApiResponse<object>);
    }
}
