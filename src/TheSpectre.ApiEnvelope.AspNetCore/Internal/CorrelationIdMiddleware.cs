using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Resolves the correlation id, echoes it, and scopes it into the logger.</summary>
internal sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiEnvelopeOptions _options;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        IOptions<ApiEnvelopeOptions> options,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var inbound = context.Request.Headers[_options.CorrelationIdHeaderName].ToString();
        var correlationId = CorrelationId.Resolve(inbound);

        context.Items[CorrelationId.ItemsKey] = correlationId;
        context.Response.Headers[_options.CorrelationIdHeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }
}
