using Microsoft.AspNetCore.Http;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Maps an exception to the status code and error key the envelope will carry.</summary>
internal static class ExceptionStatusMap
{
    /// <summary>Resolves the status code and error key for <paramref name="exception"/>.</summary>
    internal static (int StatusCode, string ErrorCode) Resolve(
        Exception exception,
        ApiEnvelopeOptions options)
    {
        if (exception is AppException appException)
        {
            return (appException.StatusCode, appException.ErrorCode);
        }

        if (exception is BadHttpRequestException badRequest)
        {
            // Route through the same table as everything else. Hard-coding BadRequest here
            // would make a thrown BadHttpRequestException(413) emit BAD_REQUEST while a
            // routing-level 413 emits PAYLOAD_TOO_LARGE — one status, two keys, depending on
            // which path produced it. That is the branching this library exists to remove.
            return (badRequest.StatusCode, StatusCodeErrorCodes.ForStatus(badRequest.StatusCode));
        }

        var mapped = FindMostDerivedMapping(exception.GetType(), options);

        return mapped is null
            ? (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError)
            : (mapped.Value, StatusCodeErrorCodes.ForStatus(mapped.Value));
    }

    // Walks from the exception's own type up through its base types, so the most derived
    // mapping always wins regardless of dictionary iteration order.
    private static int? FindMostDerivedMapping(Type exceptionType, ApiEnvelopeOptions options)
    {
        for (var type = exceptionType; type is not null; type = type.BaseType)
        {
            if (options.ExceptionStatusCodes.TryGetValue(type, out var statusCode))
            {
                return statusCode;
            }
        }

        return null;
    }
}
