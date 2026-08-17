using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>
/// Rewrites an MVC action's <c>ProducesResponseTypeAttribute</c> filters so <c>ApiExplorer</c>
/// — and therefore every OpenAPI generator built on it — describes the envelope the result
/// filter applies at runtime, rather than the bare type the action declared.
/// </summary>
/// <remarks>
/// The type decision is the same one <see cref="EnvelopeResponseMetadata"/> makes for minimal
/// APIs: a 2xx type becomes <c>ApiResponse&lt;T&gt;</c>, 204 becomes 200, and missing 400/500
/// entries are added as <c>ApiResponse&lt;object&gt;</c>. Both call the same
/// <see cref="EnvelopeResponseMetadata.WrapSuccessType"/>, <see cref="EnvelopeResponseMetadata.NormaliseStatusCode"/>,
/// <see cref="EnvelopeResponseMetadata.IsSuccessStatusCode"/> and
/// <see cref="EnvelopeResponseMetadata.ErrorPayloadType"/> members, so a controller action and a
/// minimal-API handler returning the same type describe the same metadata.
/// </remarks>
internal sealed class EnvelopeApiResponseConvention : IActionModelConvention
{
    /// <summary>Rewrites <paramref name="action"/>'s response-type filters to describe the envelope.</summary>
    public void Apply(ActionModel action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // NoEnvelopeAttribute can sit on the action or on its controller (Inherited = true
        // covers derived controllers too); either one means this action is never enveloped at
        // runtime, so its declared response types must stay exactly as written.
        if (action.Attributes.OfType<NoEnvelopeAttribute>().Any()
            || action.Controller.Attributes.OfType<NoEnvelopeAttribute>().Any())
        {
            return;
        }

        var statusCodes = RewriteSuccessFilters(action.Filters);
        AddMissingErrorFilters(action.Filters, statusCodes);
    }

    private static HashSet<int> RewriteSuccessFilters(IList<IFilterMetadata> filters)
    {
        var statusCodes = new HashSet<int>();

        // ToList: filters is mutated inside the loop, so it cannot be enumerated live.
        foreach (var filter in filters.OfType<ProducesResponseTypeAttribute>().ToList())
        {
            if (!EnvelopeResponseMetadata.IsSuccessStatusCode(filter.StatusCode))
            {
                statusCodes.Add(filter.StatusCode);
                continue;
            }

            filters.Remove(filter);

            var normalisedStatusCode = EnvelopeResponseMetadata.NormaliseStatusCode(filter.StatusCode);
            filters.Add(new ProducesResponseTypeAttribute(
                EnvelopeResponseMetadata.WrapSuccessType(filter.Type), normalisedStatusCode));
            statusCodes.Add(normalisedStatusCode);
        }

        return statusCodes;
    }

    private static void AddMissingErrorFilters(IList<IFilterMetadata> filters, ISet<int> statusCodes)
    {
        if (statusCodes.Add(StatusCodes.Status400BadRequest))
        {
            filters.Add(new ProducesResponseTypeAttribute(
                EnvelopeResponseMetadata.ErrorPayloadType, StatusCodes.Status400BadRequest));
        }

        if (statusCodes.Add(StatusCodes.Status500InternalServerError))
        {
            filters.Add(new ProducesResponseTypeAttribute(
                EnvelopeResponseMetadata.ErrorPayloadType, StatusCodes.Status500InternalServerError));
        }
    }
}
