using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Envelopes the value a minimal-API handler returns.</summary>
/// <remarks>
/// Works on the typed return value, never on bytes. <see cref="IValueHttpResult"/> and
/// <see cref="IStatusCodeHttpResult"/> are the seams ASP.NET Core provides for reading a
/// result's payload and status without knowing its concrete type — so a
/// <c>FileHttpResult</c> or a redirect, which implement neither in a way we can unwrap, is
/// skipped by type test rather than by path pattern.
/// </remarks>
internal sealed class ApiEnvelopeEndpointFilter : IEndpointFilter
{
    private readonly ApiEnvelopeOptions _options;
    private readonly IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> _json;

    public ApiEnvelopeEndpointFilter(
        IOptions<ApiEnvelopeOptions> options,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json)
    {
        _options = options.Value;
        _json = json;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var returned = await next(context);
        var httpContext = context.HttpContext;

        if (!TryUnwrap(returned, out var value, out var statusCode))
        {
            return returned;
        }

        if (SuccessEnvelope.ShouldWrap(httpContext, _options, value, statusCode))
        {
            return new EnvelopeHttpResult(value, statusCode, _options, _json.Value.SerializerOptions);
        }

        if (!SuccessEnvelope.IsSuccessStatus(statusCode)
            && value is not IApiResponse
            && !EnvelopeBypass.ShouldBypass(httpContext, _options))
        {
            // A result with status >= 400 must never be success-wrapped (see
            // SuccessEnvelope.IsSuccessStatus) - route it to the error envelope instead,
            // discarding whatever value it carried. NoEnvelope and an already-built
            // ApiResponse both still opt out.
            return new ErrorEnvelopeHttpResult(statusCode, _options, _json.Value.SerializerOptions);
        }

        return returned;
    }

    // Returns false for results this library must not touch: files, redirects, challenges,
    // and anything else that carries no unwrappable payload.
    private static bool TryUnwrap(object? returned, out object? value, out int statusCode)
    {
        switch (returned)
        {
            case IValueHttpResult valueResult:
                value = valueResult.Value;
                statusCode = (returned as IStatusCodeHttpResult)?.StatusCode
                    ?? StatusCodes.Status200OK;
                return true;

            case IStatusCodeHttpResult { StatusCode: StatusCodes.Status204NoContent }:
                value = null;
                statusCode = StatusCodes.Status204NoContent;
                return true;

            // A void/Task handler and TypedResults.Empty are all normalised by the framework to
            // the same EmptyHttpResult singleton before any filter sees them. MVC's equivalent
            // (an action returning void, or an explicit EmptyResult) is always wrapped, so this
            // must be too, for MVC/minimal-API parity.
            case Microsoft.AspNetCore.Http.HttpResults.EmptyHttpResult:
                value = null;
                statusCode = StatusCodes.Status200OK;
                return true;

            // Created(uri)/Accepted(uri) with no value implement IStatusCodeHttpResult but not
            // IValueHttpResult - structurally identical to Ok()/BadRequest()/NotFound() with no
            // value, which stay in the IResult fall-through below. They are matched by exact
            // type, not by interface, because MVC only wraps them by an accident of its own
            // class hierarchy: CreatedResult/AcceptedResult derive from ObjectResult regardless
            // of whether a value was supplied. Matching that accident - not rationalising it -
            // is what MVC/minimal-API parity means here.
            case Microsoft.AspNetCore.Http.HttpResults.Created:
            case Microsoft.AspNetCore.Http.HttpResults.Accepted:
                value = null;
                statusCode = ((IStatusCodeHttpResult)returned).StatusCode ?? StatusCodes.Status200OK;
                return true;

            // TypedResults.Ok()/Results.StatusCode(n) with no value ship with an empty body
            // today, forcing a client to check the status before daring to read it. Matched by
            // exact type, not by IStatusCodeHttpResult alone: RedirectHttpResult implements that
            // interface too, and a redirect carrying a JSON body is broken because its Location
            // header is the entire point - the same judgement already recorded above for
            // Created/Accepted. Restricted further to 2xx only, deliberately: do not reuse
            // SuccessEnvelope.IsSuccessStatus here, since it spans 200-399 and
            // Results.StatusCode(302) is this same type carrying a redirect-shaped status.
            case Microsoft.AspNetCore.Http.HttpResults.Ok:
            case Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult:
                var bodilessStatus = ((IStatusCodeHttpResult)returned).StatusCode
                    ?? StatusCodes.Status200OK;
                if (bodilessStatus is < 200 or > 299)
                {
                    value = null;
                    statusCode = 0;
                    return false;
                }

                value = null;
                statusCode = bodilessStatus;
                return true;

            case IResult:
                // A file, a redirect, a challenge, or a bodiless result MVC also leaves alone
                // (Ok(), BadRequest(), NotFound(), ...) - leave it exactly as it is.
                value = null;
                statusCode = 0;
                return false;

            default:
                value = returned;
                statusCode = StatusCodes.Status200OK;
                return true;
        }
    }
}
