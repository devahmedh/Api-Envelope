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

        if (!SuccessEnvelope.ShouldWrap(httpContext, _options, value))
        {
            return returned;
        }

        return new EnvelopeHttpResult(value, statusCode, _options, _json.Value.SerializerOptions);
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

            case IResult:
                // A file, a redirect, a challenge — leave it exactly as it is.
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
