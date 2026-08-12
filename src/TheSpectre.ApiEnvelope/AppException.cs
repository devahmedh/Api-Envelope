namespace TheSpectre.ApiEnvelope;

/// <summary>
/// The exception any layer throws to produce a specific error envelope.
/// </summary>
/// <remarks>
/// Not sealed: domain-specific subclasses such as
/// <c>public sealed class ProjectCodeTakenException : AppException</c> keep the error key at
/// the throw site while giving the domain a named type. The inner exception is always
/// preserved, so a caller recovering from an EF concurrency conflict can still reach
/// <c>ex.Entries</c>.
/// </remarks>
public class AppException : Exception
{
    /// <summary>Initialises a new <see cref="AppException"/>.</summary>
    /// <param name="errorCode">The stable SCREAMING_SNAKE_CASE key the client switches on.</param>
    /// <param name="message">Diagnostics only — for logs. Never rendered to a user.</param>
    /// <param name="statusCode">The HTTP status code to return. Defaults to 400.</param>
    /// <param name="innerException">The originating exception, preserved.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="errorCode"/> is null, empty or whitespace.
    /// </exception>
    public AppException(
        string errorCode,
        string? message = null,
        int statusCode = 400,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    /// <summary>Initialises a new <see cref="AppException"/> carrying field-level details.</summary>
    /// <param name="errorCode">The stable key the client switches on.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="details">The field-level failures.</param>
    /// <param name="message">Diagnostics only — for logs. Never rendered to a user.</param>
    /// <param name="innerException">The originating exception, preserved.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="errorCode"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="details"/> is null.</exception>
    public AppException(
        string errorCode,
        int statusCode,
        IReadOnlyList<ErrorDetail> details,
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentNullException.ThrowIfNull(details);

        ErrorCode = errorCode;
        StatusCode = statusCode;
        Details = details;
    }

    /// <summary>The stable key the client switches on.</summary>
    public string ErrorCode { get; }

    /// <summary>The HTTP status code to return.</summary>
    public int StatusCode { get; }

    /// <summary>Field-level failures, or <see langword="null"/>.</summary>
    public IReadOnlyList<ErrorDetail>? Details { get; }
}
