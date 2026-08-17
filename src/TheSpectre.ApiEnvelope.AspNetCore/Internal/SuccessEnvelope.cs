using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>The single wrap decision and the single success write path.</summary>
/// <remarks>
/// Both the MVC result filter and the minimal-API endpoint filter route through here, so a
/// controller action and a minimal-API handler returning the same value produce byte-identical
/// responses. Nothing here inspects the response body — the decision is made on the typed
/// value, before anything is serialised.
/// </remarks>
internal static class SuccessEnvelope
{
    private const string JsonContentType = "application/json; charset=utf-8";

    /// <summary>Whether <paramref name="statusCode"/> denotes a successful outcome.</summary>
    /// <remarks>
    /// A result with status &gt;= 400 must never be success-wrapped: it belongs in the error
    /// envelope instead, with its value discarded (see <c>ErrorEnvelopeActionResult</c> and
    /// <c>ErrorEnvelopeHttpResult</c>). Otherwise <c>BadRequest(new { field = "code" })</c> — or
    /// worse, <c>[ApiController]</c>'s automatic model-validation 400 — would ship as
    /// <c>{"isSuccess":true,...}</c> at HTTP 400, and the client's success branch would never
    /// call <c>translateError</c>.
    /// </remarks>
    internal static bool IsSuccessStatus(int statusCode) =>
        statusCode is >= 200 and < 400;

    /// <summary>Whether <paramref name="value"/> should be enveloped as a success.</summary>
    internal static bool ShouldWrap(
        HttpContext context, ApiEnvelopeOptions options, object? value, int statusCode)
    {
        if (!IsSuccessStatus(statusCode))
        {
            return false;
        }

        // A type test, not a JSON inspection: a payload that merely happens to have an
        // "isSuccess" property must not be mistaken for an envelope.
        if (value is IApiResponse)
        {
            return false;
        }

        return !EnvelopeBypass.ShouldBypass(context, options);
    }

    /// <summary>
    /// Maps a success status code to the one the envelope will carry.
    /// </summary>
    /// <remarks>
    /// 204 becomes 200. HTTP forbids a body on a 204, and a body-less response is the single
    /// case where a client would have to check the status before daring to read the body — the
    /// exact branching this library exists to remove. Every other code passes through.
    /// </remarks>
    internal static int NormaliseStatusCode(int statusCode) =>
        statusCode == StatusCodes.Status204NoContent ? StatusCodes.Status200OK : statusCode;

    /// <summary>Writes a success envelope around <paramref name="value"/>.</summary>
    internal static async Task WriteAsync(
        HttpContext context,
        object? value,
        int statusCode,
        ApiEnvelopeOptions options,
        JsonSerializerOptions json)
    {
        var normalised = NormaliseStatusCode(statusCode);
        var correlationId = context.GetCorrelationId();

        var response = ApiResponse.Success(value, correlationId, normalised);
        var payload = ApiEnvelopeWriter.WriteToUtf8Bytes(response, json);

        context.Response.StatusCode = normalised;
        context.Response.ContentType = JsonContentType;
        context.Response.ContentLength = payload.Length;
        context.Response.Headers[options.CorrelationIdHeaderName] = correlationId;

        await context.Response.Body.WriteAsync(payload, context.RequestAborted);
    }
}
