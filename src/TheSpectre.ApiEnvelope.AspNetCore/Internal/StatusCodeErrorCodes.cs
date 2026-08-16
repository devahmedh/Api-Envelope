using Microsoft.AspNetCore.Http;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Maps an HTTP status code to the error key the client switches on.</summary>
/// <remarks>
/// Used both by the exception handler, for exceptions mapped by status, and by the
/// status-code middleware, for bare responses that never reached an endpoint. Sharing one
/// table is what makes a routing 404 and a thrown <see cref="KeyNotFoundException"/> produce
/// the same <c>errorCode</c>.
/// </remarks>
internal static class StatusCodeErrorCodes
{
    /// <summary>Returns the error key for <paramref name="statusCode"/>.</summary>
    internal static string ForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.BadRequest,
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
        StatusCodes.Status409Conflict => ErrorCodes.Conflict,
        StatusCodes.Status413PayloadTooLarge => ErrorCodes.PayloadTooLarge,
        StatusCodes.Status415UnsupportedMediaType => ErrorCodes.UnsupportedMediaType,
        StatusCodes.Status429TooManyRequests => ErrorCodes.TooManyRequests,
        StatusCodes.Status501NotImplemented => ErrorCodes.NotImplemented,
        StatusCodes.Status504GatewayTimeout => ErrorCodes.Timeout,
        >= 500 => ErrorCodes.InternalError,
        _ => ErrorCodes.BadRequest,
    };
}
