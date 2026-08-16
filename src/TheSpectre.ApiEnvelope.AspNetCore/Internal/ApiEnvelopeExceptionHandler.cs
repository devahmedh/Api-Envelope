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
    private readonly ILogger<ApiEnvelopeExceptionHandler> _logger;

    public ApiEnvelopeExceptionHandler(
        IOptions<ApiEnvelopeOptions> options,
        IHostEnvironment environment,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json,
        ILogger<ApiEnvelopeExceptionHandler> logger)
    {
        _options = options.Value;
        _environment = environment;
        _json = json;
        _logger = logger;
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

        if (context.Response.HasStarted || EnvelopeBypass.ShouldBypass(context, _options))
        {
            return false;
        }

        var (statusCode, errorCode) = ExceptionStatusMap.Resolve(exception, _options);

        _logger.LogError(
            exception,
            "Request {Method} {Path} failed with {ErrorCode}. CorrelationId {CorrelationId}.",
            context.Request.Method,
            context.Request.Path,
            errorCode,
            context.GetCorrelationId());

        await EnvelopeResponseWriter.WriteAsync(
            context,
            statusCode,
            errorCode,
            ResolveMessage(exception),
            (exception as AppException)?.Details,
            _json.Value.SerializerOptions);

        return true;
    }

    private string? ResolveMessage(Exception exception) => _options.MessageVisibility switch
    {
        MessageVisibility.Always => exception.Message,
        MessageVisibility.DevelopmentOnly when _environment.IsDevelopment() => exception.Message,
        _ => null,
    };
}
