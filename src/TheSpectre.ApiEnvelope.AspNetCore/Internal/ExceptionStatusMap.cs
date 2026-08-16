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
            return (badRequest.StatusCode, ErrorCodes.BadRequest);
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
