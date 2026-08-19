using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Maps an unhandled exception to the error envelope.</summary>
internal sealed class ApiEnvelopeExceptionHandler : IExceptionHandler
{
    private readonly ApiEnvelopeOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> _json;
    private readonly ILogger _logger;

    public ApiEnvelopeExceptionHandler(
        IOptions<ApiEnvelopeOptions> options,
        IHostEnvironment environment,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json,
        ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _environment = environment;
        _json = json;
        _logger = loggerFactory.CreateLogger(LoggerCategories.Exception);
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // A cancelled request has no client left to read a response. Writing to a dead socket
        // achieves nothing and 499 is not an IANA status code, so log and stop.
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request {Method} {Path} was cancelled by the client. CorrelationId {CorrelationId}.",
                context.Request.Method,
                context.Request.Path,
                context.GetCorrelationId());

            return true;
        }

        if (context.Response.HasStarted)
        {
            return false;
        }

        if (EnvelopeBypass.ShouldBypass(context, _options))
        {
            // Declining to envelope must not mean declining to record the failure: without
            // this, an exception on an excluded path (/health, /metrics, a [NoEnvelope]
            // endpoint) would leave no trace anywhere this library controls.
            _logger.LogWarning(
                exception,
                "Request {Method} {Path} failed with an unhandled exception on a bypassed path. CorrelationId {CorrelationId}.",
                context.Request.Method,
                context.Request.Path,
                context.GetCorrelationId());

            return false;
        }

        var (statusCode, errorCode) = ExceptionStatusMap.Resolve(exception, _options);

        // Not logged here. EnvelopeResponseWriter records every error envelope, so logging
        // here as well would enter each exception twice — and only the writer knows whether
        // this failure is a validation result, which belongs in a different category.
        await EnvelopeResponseWriter.WriteAsync(
            context,
            statusCode,
            errorCode,
            ResolveMessage(exception),
            (exception as AppException)?.Details,
            _json.Value.SerializerOptions,
            _options.CorrelationIdHeaderName,
            exception);

        return true;
    }

    private string? ResolveMessage(Exception exception) => _options.MessageVisibility switch
    {
        MessageVisibility.Always => exception.Message,
        MessageVisibility.DevelopmentOnly when _environment.IsDevelopment() => exception.Message,
        _ => null,
    };
}
