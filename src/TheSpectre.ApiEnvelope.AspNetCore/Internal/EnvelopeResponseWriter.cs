using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>The single place an error envelope is written to an <see cref="HttpResponse"/>.</summary>
/// <remarks>
/// Both the exception handler and the status-code middleware route through here, so an
/// unhandled exception and a bare 401 produce byte-identical envelope structure — and, since
/// this is also where the failure is recorded, one consistent log entry.
/// </remarks>
internal static class EnvelopeResponseWriter
{
    private const string JsonContentType = "application/json; charset=utf-8";

    /// <summary>Writes an error envelope, sets the status code and content type, and logs it.</summary>
    /// <param name="context">The current request context.</param>
    /// <param name="statusCode">The HTTP status code to write.</param>
    /// <param name="errorCode">The stable key the client switches on.</param>
    /// <param name="message">Diagnostics only — for logs. Never rendered to a user.</param>
    /// <param name="details">Field-level failures, or <see langword="null"/>.</param>
    /// <param name="json">The serializer options to write the envelope with.</param>
    /// <param name="correlationIdHeaderName">The header name the correlation id is echoed on.</param>
    /// <param name="exception">
    /// The exception behind the failure, when there is one. Supplied only by the exception
    /// handler; the status-code middleware and the error result types pass nothing.
    /// </param>
    internal static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string errorCode,
        string? message,
        IReadOnlyList<ErrorDetail>? details,
        JsonSerializerOptions json,
        string correlationIdHeaderName,
        Exception? exception = null)
    {
        var correlationId = context.GetCorrelationId();

        TryLog(context, statusCode, errorCode, details, exception, correlationId);

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

    /// <summary>
    /// Runs <see cref="Log"/> with every failure isolated from it. Logging is a diagnostic aid,
    /// not part of the contract with the caller: <see cref="WriteAsync"/> is the single place an
    /// error envelope is written, and nothing about recording the failure — a null
    /// <see cref="HttpContext.RequestServices"/>, or a host-registered
    /// <see cref="ILoggerProvider"/> that throws — may be allowed to cost the caller their
    /// response.
    /// </summary>
    private static void TryLog(
        HttpContext context,
        int statusCode,
        string errorCode,
        IReadOnlyList<ErrorDetail>? details,
        Exception? exception,
        string correlationId)
    {
        try
        {
            Log(context, statusCode, errorCode, details, exception, correlationId);
        }
        catch (Exception)
        {
            // There is nowhere to report a logging failure except the logging system that just
            // failed, and the caller's error response matters more than the diagnostic record
            // of it. Swallowed deliberately: see the remarks on TryLog.
        }
    }

    /// <summary>
    /// Routes the failure to one of three categories. The discriminator is the error code, not
    /// the exception type: a validation failure arrives as a thrown <see cref="AppException"/>
    /// too, and classifying it by type would keep a user's form mistake in the error stream.
    /// </summary>
    private static void Log(
        HttpContext context,
        int statusCode,
        string errorCode,
        IReadOnlyList<ErrorDetail>? details,
        Exception? exception,
        string correlationId)
    {
        var loggers = context.RequestServices?.GetService<EnvelopeLoggers>();

        if (loggers is null)
        {
            return;
        }

        if (errorCode == ErrorCodes.ValidationFailed && details is { Count: > 0 })
        {
            var validation = loggers.Validation;

            // Checked before formatting: without this the summary string is built and thrown
            // away on every request the category is silenced for.
            if (!validation.IsEnabled(LogLevel.Information))
            {
                return;
            }

            // No exception is attached. The stack trace of an AppException thrown from a
            // validation filter records where the library threw, never why the input failed.
            validation.LogInformation(
                "Request {Method} {Path} failed validation with {DetailCount} detail(s): {ErrorCodes}. CorrelationId {CorrelationId}.",
                context.Request.Method,
                context.Request.Path,
                details.Count,
                Summarise(details),
                correlationId);

            return;
        }

        if (exception is not null)
        {
            var failure = loggers.Exception;

            if (!failure.IsEnabled(LogLevel.Error))
            {
                return;
            }

            failure.LogError(
                exception,
                "Request {Method} {Path} failed with {ErrorCode}. CorrelationId {CorrelationId}.",
                context.Request.Method,
                context.Request.Path,
                errorCode,
                correlationId);

            return;
        }

        var status = loggers.StatusCode;

        if (!status.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        status.LogDebug(
            "Request {Method} {Path} returned {StatusCode} {ErrorCode}. CorrelationId {CorrelationId}.",
            context.Request.Method,
            context.Request.Path,
            statusCode,
            errorCode,
            correlationId);
    }

    /// <summary>
    /// Field and key only. <see cref="ErrorDetail.Params"/> carries the constraint bound rather
    /// than the submitted value, but it is left out entirely so that no future change to what a
    /// caller puts there can reach a log.
    /// </summary>
    private static string Summarise(IReadOnlyList<ErrorDetail> details) =>
        string.Join(", ", details.Select(detail => $"{detail.Field}={detail.ErrorCode}"));
}
