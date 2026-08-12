namespace TheSpectre.ApiEnvelope;

/// <summary>
/// The non-generic view of a response envelope.
/// </summary>
/// <remarks>
/// Middleware and filters use this to detect an already-enveloped value by type test rather
/// than by inspecting serialized JSON for an <c>isSuccess</c> property, which would
/// false-positive on any payload that legitimately has one.
/// </remarks>
public interface IApiResponse
{
    /// <summary>Whether the request succeeded.</summary>
    bool IsSuccess { get; }

    /// <summary>The HTTP status code carried in the body.</summary>
    int StatusCode { get; }

    /// <summary>The stable error key, or <see langword="null"/> on success.</summary>
    string? ErrorCode { get; }

    /// <summary>The correlation id echoed on every response.</summary>
    string CorrelationId { get; }
}
