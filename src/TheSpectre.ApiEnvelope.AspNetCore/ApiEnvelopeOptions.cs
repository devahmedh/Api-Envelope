using Microsoft.AspNetCore.Http;

namespace TheSpectre.ApiEnvelope.AspNetCore;

/// <summary>When the diagnostics-only <c>message</c> field is written to the wire.</summary>
public enum MessageVisibility
{
    /// <summary>Never emit <c>message</c>, in any environment.</summary>
    Never = 0,

    /// <summary>Emit <c>message</c> only when the host environment is Development.</summary>
    DevelopmentOnly = 1,

    /// <summary>Always emit <c>message</c>.</summary>
    Always = 2,
}

/// <summary>Configuration for the API envelope middleware.</summary>
/// <remarks>
/// Deliberately small. Every option is a permanent maintenance obligation, and the components
/// never inspect the response body, so none of the path-sniffing options a buffering wrapper
/// needs exist here.
/// </remarks>
public sealed class ApiEnvelopeOptions
{
    /// <summary>
    /// Request path prefixes that bypass the envelope entirely.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>/swagger</c>, <c>/health</c>, <c>/metrics</c> and <c>/hubs</c>.
    /// SignalR negotiation in particular breaks if its response is altered.
    /// </remarks>
    public IList<PathString> ExcludedPathPrefixes { get; } = new List<PathString>
    {
        new("/swagger"),
        new("/health"),
        new("/metrics"),
        new("/hubs"),
    };

    /// <summary>When the diagnostics-only <c>message</c> field is emitted.</summary>
    /// <remarks>
    /// Defaults to <see cref="MessageVisibility.DevelopmentOnly"/>, so an unrecognised
    /// exception cannot leak its text in production by construction rather than by convention.
    /// </remarks>
    public MessageVisibility MessageVisibility { get; set; } = MessageVisibility.DevelopmentOnly;

    /// <summary>The request and response header carrying the correlation id.</summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>Exception type to HTTP status code. Consumers may add to or remove from it.</summary>
    /// <remarks>
    /// <see cref="AppException"/> is not listed: it carries its own status code.
    /// <see cref="OperationCanceledException"/> is not listed either — a cancelled request has
    /// no client left to read a response, so nothing is written.
    /// Lookup walks the type hierarchy, so <see cref="ArgumentNullException"/> resolves through
    /// <see cref="ArgumentException"/>.
    /// </remarks>
    public IDictionary<Type, int> ExceptionStatusCodes { get; } = new Dictionary<Type, int>
    {
        [typeof(TimeoutException)] = StatusCodes.Status504GatewayTimeout,
        [typeof(NotImplementedException)] = StatusCodes.Status501NotImplemented,
        [typeof(KeyNotFoundException)] = StatusCodes.Status404NotFound,
        [typeof(ArgumentException)] = StatusCodes.Status400BadRequest,
    };
}
