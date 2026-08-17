using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Envelopes the result of an MVC action.</summary>
/// <remarks>
/// Implements <see cref="IAsyncAlwaysRunResultFilter"/>, not <see cref="IAsyncResultFilter"/>.
/// A plain result filter is skipped when an earlier filter short-circuits the pipeline — which
/// is exactly what a validation filter does on an invalid model. With the plain interface,
/// every validation failure would ship un-enveloped: the most common error response in the API
/// would be the one breaking the contract.
/// </remarks>
internal sealed class ApiEnvelopeResultFilter : IAsyncAlwaysRunResultFilter
{
    private readonly ApiEnvelopeOptions _options;
    private readonly IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> _json;

    public ApiEnvelopeResultFilter(
        IOptions<ApiEnvelopeOptions> options,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json)
    {
        _options = options.Value;
        _json = json;
    }

    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (TryUnwrap(context.Result, out var value, out var statusCode))
        {
            if (SuccessEnvelope.ShouldWrap(context.HttpContext, _options, value, statusCode))
            {
                context.Result = new EnvelopeActionResult(
                    value, statusCode, _options, _json.Value.SerializerOptions);
            }
            else if (!SuccessEnvelope.IsSuccessStatus(statusCode)
                && value is not IApiResponse
                && !EnvelopeBypass.ShouldBypass(context.HttpContext, _options))
            {
                // A result with status >= 400 must never be success-wrapped (see
                // SuccessEnvelope.IsSuccessStatus) - route it to the error envelope instead,
                // discarding whatever value it carried (ValidationProblemDetails, an anonymous
                // object, ...). NoEnvelope and an already-built ApiResponse both still opt out.
                context.Result = new ErrorEnvelopeActionResult(
                    statusCode, _options, _json.Value.SerializerOptions);
            }
        }

        await next();
    }

    // A FileResult, a RedirectResult or a ChallengeResult is skipped by type test, which is
    // what wrapping at the result layer buys: no path patterns, no body sniffing.
    private static bool TryUnwrap(IActionResult result, out object? value, out int statusCode)
    {
        switch (result)
        {
            case ObjectResult objectResult:
                value = objectResult.Value;
                statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
                return true;

            // JsonResult is not an ObjectResult - it needs its own case for MVC/minimal-API
            // parity, since Results.Json(...) on the minimal side is already unwrapped.
            case JsonResult jsonResult:
                value = jsonResult.Value;
                statusCode = jsonResult.StatusCode ?? StatusCodes.Status200OK;
                return true;

            case StatusCodeResult { StatusCode: StatusCodes.Status204NoContent }:
            case EmptyResult:
                value = null;
                statusCode = StatusCodes.Status204NoContent;
                return true;

            // Any other bodiless StatusCodeResult - Ok() with no value, StatusCode(202), ... -
            // ships with an empty body today, forcing a client to check the status before
            // daring to read it. 2xx only, deliberately: SuccessEnvelope.IsSuccessStatus covers
            // 200-399, which includes redirects, and a redirect carrying a JSON body is broken
            // because its Location header is the entire point. Do not widen this to that helper.
            case StatusCodeResult { StatusCode: >= 200 and <= 299 } statusCodeResult:
                value = null;
                statusCode = statusCodeResult.StatusCode;
                return true;

            default:
                value = null;
                statusCode = 0;
                return false;
        }
    }
}
