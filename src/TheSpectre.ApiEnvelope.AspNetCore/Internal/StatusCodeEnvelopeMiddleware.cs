using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Envelopes error responses that never reached an endpoint.</summary>
/// <remarks>
/// Covers the 401 challenge, 403, routing 404, 405, 415 and Kestrel-level 413/431 — responses
/// produced by middleware that short-circuits before any filter can see them. Without this,
/// those ship with an empty body and the client still has to branch on response shape, which
/// defeats the library's whole purpose.
/// </remarks>
internal sealed class StatusCodeEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiEnvelopeOptions _options;
    private readonly IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> _json;

    public StatusCodeEnvelopeMiddleware(
        RequestDelegate next,
        IOptions<ApiEnvelopeOptions> options,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json)
    {
        _next = next;
        _options = options.Value;
        _json = json;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        finally
        {
            // This middleware sits between the exception handler and UseRouting, so it is the
            // last point in the pipeline where the endpoint UseRouting matched is still visible
            // when the endpoint throws: ExceptionHandlerMiddlewareImpl.ClearHttpContext() calls
            // SetEndpoint(null) before any IExceptionHandler runs. A finally block executes
            // during that unwind too (not just on normal return), so this is the one place both
            // paths — a bare status code and a thrown exception — can stash it.
            if (context.GetEndpoint() is { } endpoint)
            {
                context.Items[EnvelopeBypass.EndpointItemsKey] = endpoint;
            }
        }

        // HasStarted == false proves no bytes were written: there is nothing to buffer and
        // nothing to sniff, so this can never corrupt a response another component produced.
        if (context.Response.HasStarted
            || context.Response.StatusCode < StatusCodes.Status400BadRequest
            || context.Response.ContentLength is not (null or 0)
            || EnvelopeBypass.ShouldBypass(context, _options))
        {
            return;
        }

        await EnvelopeResponseWriter.WriteAsync(
            context,
            context.Response.StatusCode,
            StatusCodeErrorCodes.ForStatus(context.Response.StatusCode),
            message: null,
            details: null,
            _json.Value.SerializerOptions,
            _options.CorrelationIdHeaderName);
    }
}
