using System.Text.Json.Serialization;

namespace TheSpectre.ApiEnvelope;

/// <summary>
/// The unified response envelope. Every response — success and error alike — uses this shape.
/// </summary>
/// <typeparam name="T">The payload type carried in <see cref="Data"/>.</typeparam>
/// <remarks>
/// On the HTTP path this type is written by <c>ApiEnvelopeWriter</c> rather than by
/// reflection. The JSON attributes below exist so the shape stays correct when a consumer
/// serializes the type directly — they defend against a host application's
/// <c>PropertyNamingPolicy</c>, <c>DefaultIgnoreCondition</c> and property reordering.
/// </remarks>
public sealed class ApiResponse<T> : IApiResponse
{
    /// <summary>Whether the request succeeded.</summary>
    [JsonPropertyName("isSuccess")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required bool IsSuccess { get; init; }

    /// <summary>The HTTP status code, repeated in the body.</summary>
    [JsonPropertyName("statusCode")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int StatusCode { get; init; }

    /// <summary>The payload on success; <see langword="null"/> on error. Always present.</summary>
    [JsonPropertyName("data")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public T? Data { get; init; }

    /// <summary>The stable error key the client switches on. Always present.</summary>
    [JsonPropertyName("errorCode")]
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Diagnostics only — for logs. Never render this to a user. Omitted when null.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    /// <summary>The correlation id, echoed on every response.</summary>
    [JsonPropertyName("correlationId")]
    [JsonPropertyOrder(6)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required string CorrelationId { get; init; }

    /// <summary>Field-level failures. Omitted when null.</summary>
    [JsonPropertyName("details")]
    [JsonPropertyOrder(7)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ErrorDetail>? Details { get; init; }
}

/// <summary>Factory methods for <see cref="ApiResponse{T}"/>.</summary>
public static class ApiResponse
{
    /// <summary>Creates a success envelope.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="data">The payload.</param>
    /// <param name="correlationId">The correlation id for this request.</param>
    /// <param name="statusCode">The HTTP status code. Defaults to 200.</param>
    public static ApiResponse<T> Success<T>(T data, string correlationId, int statusCode = 200) =>
        new()
        {
            IsSuccess = true,
            StatusCode = statusCode,
            Data = data,
            ErrorCode = null,
            CorrelationId = correlationId,
        };

    /// <summary>Creates an error envelope.</summary>
    /// <param name="errorCode">The stable error key.</param>
    /// <param name="correlationId">The correlation id for this request.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="message">Diagnostics only. Never rendered to a user.</param>
    /// <param name="details">Field-level failures, if any.</param>
    public static ApiResponse<object?> Failure(
        string errorCode,
        string correlationId,
        int statusCode,
        string? message = null,
        IReadOnlyList<ErrorDetail>? details = null) =>
        new()
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Data = null,
            ErrorCode = errorCode,
            Message = message,
            CorrelationId = correlationId,
            Details = details,
        };
}
