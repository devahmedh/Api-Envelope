using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>An <see cref="IActionResult"/> that writes an error envelope for a bare 4xx/5xx result.</summary>
/// <remarks>
/// The original value is deliberately discarded, not forwarded. The envelope has no slot for an
/// arbitrary error payload — only <c>errorCode</c>, an optional diagnostics-only <c>message</c>
/// and optional <c>details</c>. Shipping a <c>ValidationProblemDetails</c> or similar through
/// this slot would put backend-authored English on the wire, which the key-not-string contract
/// forbids outright. Throwing <see cref="AppException"/> with <see cref="ErrorDetail"/>s is the
/// supported way to attach error information; the exception handler already renders it
/// correctly.
/// </remarks>
internal sealed class ErrorEnvelopeActionResult : IActionResult
{
    private readonly int _statusCode;
    private readonly ApiEnvelopeOptions _options;
    private readonly JsonSerializerOptions _json;

    internal ErrorEnvelopeActionResult(int statusCode, ApiEnvelopeOptions options, JsonSerializerOptions json)
    {
        _statusCode = statusCode;
        _options = options;
        _json = json;
    }

    public Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return EnvelopeResponseWriter.WriteAsync(
            context.HttpContext,
            _statusCode,
            StatusCodeErrorCodes.ForStatus(_statusCode),
            message: null,
            details: null,
            _json,
            _options.CorrelationIdHeaderName);
    }
}
