using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>An <see cref="IResult"/> that writes an error envelope for a bare 4xx/5xx result.</summary>
/// <remarks>
/// See <see cref="ErrorEnvelopeActionResult"/> for why the original value is discarded rather
/// than forwarded — the same reasoning applies here, so a controller action and a minimal-API
/// handler that both return a 4xx result produce the same error envelope.
/// </remarks>
internal sealed class ErrorEnvelopeHttpResult : IResult
{
    private readonly int _statusCode;
    private readonly ApiEnvelopeOptions _options;
    private readonly JsonSerializerOptions _json;

    internal ErrorEnvelopeHttpResult(int statusCode, ApiEnvelopeOptions options, JsonSerializerOptions json)
    {
        _statusCode = statusCode;
        _options = options;
        _json = json;
    }

    public Task ExecuteAsync(HttpContext httpContext) =>
        EnvelopeResponseWriter.WriteAsync(
            httpContext,
            _statusCode,
            StatusCodeErrorCodes.ForStatus(_statusCode),
            message: null,
            details: null,
            _json,
            _options.CorrelationIdHeaderName);
}
