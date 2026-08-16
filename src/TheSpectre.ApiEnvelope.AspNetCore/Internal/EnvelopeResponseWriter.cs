using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>The single place an error envelope is written to an <see cref="HttpResponse"/>.</summary>
/// <remarks>
/// Both the exception handler and the status-code middleware route through here, so an
/// unhandled exception and a bare 401 produce byte-identical envelope structure.
/// </remarks>
internal static class EnvelopeResponseWriter
{
    private const string JsonContentType = "application/json; charset=utf-8";

    /// <summary>Writes an error envelope and sets the status code and content type.</summary>
    internal static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string errorCode,
        string? message,
        IReadOnlyList<ErrorDetail>? details,
        JsonSerializerOptions json,
        string correlationIdHeaderName)
    {
        var correlationId = context.GetCorrelationId();

        var response = ApiResponse.Failure(
            errorCode,
            correlationId,
            statusCode,
            message,
            details);

        var payload = ApiEnvelopeWriter.WriteToUtf8Bytes(response, json);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = JsonContentType;
        context.Response.ContentLength = payload.Length;

        // The exception-handler middleware calls HttpResponse.Clear() before any
        // IExceptionHandler runs, which wipes the header CorrelationIdMiddleware set before
        // the request ever reached the endpoint. Re-set it here, in the one place every
        // error envelope — thrown exception or bare status code alike — passes through.
        context.Response.Headers[correlationIdHeaderName] = correlationId;

        await context.Response.Body.WriteAsync(payload, context.RequestAborted);
    }
}
