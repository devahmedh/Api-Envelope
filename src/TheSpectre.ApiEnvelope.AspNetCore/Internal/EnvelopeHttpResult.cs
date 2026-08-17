using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>An <see cref="IResult"/> that writes a success envelope.</summary>
internal sealed class EnvelopeHttpResult : IResult
{
    private readonly object? _value;
    private readonly int _statusCode;
    private readonly ApiEnvelopeOptions _options;
    private readonly JsonSerializerOptions _json;

    internal EnvelopeHttpResult(
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

    public Task ExecuteAsync(HttpContext httpContext) =>
        SuccessEnvelope.WriteAsync(httpContext, _value, _statusCode, _options, _json);
}
