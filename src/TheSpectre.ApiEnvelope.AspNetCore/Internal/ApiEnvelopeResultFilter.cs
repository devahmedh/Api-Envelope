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
        if (TryUnwrap(context.Result, out var value, out var statusCode)
            && SuccessEnvelope.ShouldWrap(context.HttpContext, _options, value))
        {
            context.Result = new EnvelopeActionResult(
                value, statusCode, _options, _json.Value.SerializerOptions);
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

            case StatusCodeResult { StatusCode: StatusCodes.Status204NoContent }:
            case EmptyResult:
                value = null;
                statusCode = StatusCodes.Status204NoContent;
                return true;

            default:
                value = null;
                statusCode = 0;
                return false;
        }
    }
}
