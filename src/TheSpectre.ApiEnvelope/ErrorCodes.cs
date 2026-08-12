namespace TheSpectre.ApiEnvelope;

/// <summary>
/// Transport-level error keys used at the top level of the envelope.
/// </summary>
/// <remarks>
/// These are <c>const</c> so they can be used in <c>switch</c> case labels and attribute
/// arguments. Consumers' compilers inline the literal values, so changing a value here has
/// no effect until they recompile — which is why any change to a value is a major version bump.
/// </remarks>
public static class ErrorCodes
{
    /// <summary>An unhandled server-side failure.</summary>
    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>One or more fields failed validation; see <c>details</c>.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>The request was malformed.</summary>
    public const string BadRequest = "BAD_REQUEST";

    /// <summary>Authentication is required or failed.</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>The caller is authenticated but not permitted.</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>The requested resource does not exist.</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>The HTTP method is not allowed for this route.</summary>
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";

    /// <summary>The request conflicts with current state.</summary>
    public const string Conflict = "CONFLICT";

    /// <summary>The request body media type is not supported.</summary>
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";

    /// <summary>The request body exceeded the permitted size.</summary>
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";

    /// <summary>The caller exceeded a rate limit.</summary>
    public const string TooManyRequests = "TOO_MANY_REQUESTS";

    /// <summary>An upstream operation timed out.</summary>
    public const string Timeout = "TIMEOUT";

    /// <summary>The endpoint is not implemented.</summary>
    public const string NotImplemented = "NOT_IMPLEMENTED";
}
