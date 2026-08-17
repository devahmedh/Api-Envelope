using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>An <see cref="IActionResult"/> that writes a success envelope.</summary>
/// <remarks>
/// Deliberately bypasses MVC's output formatters and writes JSON directly. Content negotiation
/// is an explicit non-goal of this library — the envelope is always <c>application/json</c>.
/// </remarks>
internal sealed class EnvelopeActionResult : IActionResult
{
    private readonly object? _value;
    private readonly int _statusCode;
    private readonly ApiEnvelopeOptions _options;
    private readonly JsonSerializerOptions _json;

    internal EnvelopeActionResult(
        object? value,
        int statusCode,
        ApiEnvelopeOptions options,
        JsonSerializerOptions json)
    {
        _value = value;
        _statusCode = statusCode;
        _options = options;
        _json = json;
    }

    public Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return SuccessEnvelope.WriteAsync(context.HttpContext, _value, _statusCode, _options, _json);
    }
}
