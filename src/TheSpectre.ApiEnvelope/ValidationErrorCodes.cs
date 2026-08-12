namespace TheSpectre.ApiEnvelope;

/// <summary>
/// Field-level error keys used inside <c>details[].errorCode</c>.
/// </summary>
/// <remarks>
/// These live in the core package deliberately: both the FluentValidation and the
/// DataAnnotations integrations map to these same constants, so the two cannot disagree on
/// spelling and the client has exactly one contract.
/// </remarks>
public static class ValidationErrorCodes
{
    /// <summary>The field is required and was missing or empty.</summary>
    public const string Required = "REQUIRED";

    /// <summary>The value exceeded its maximum length. Params: <c>max</c>.</summary>
    public const string TooLong = "TOO_LONG";

    /// <summary>The value was below its minimum length. Params: <c>min</c>.</summary>
    public const string TooShort = "TOO_SHORT";

    /// <summary>The value fell outside its permitted range. Params: <c>min</c>, <c>max</c>.</summary>
    public const string OutOfRange = "OUT_OF_RANGE";

    /// <summary>The value did not match the required format.</summary>
    public const string InvalidFormat = "INVALID_FORMAT";

    /// <summary>The value is not a valid email address.</summary>
    public const string InvalidEmail = "INVALID_EMAIL";

    /// <summary>The value is not a valid URL.</summary>
    public const string InvalidUrl = "INVALID_URL";

    /// <summary>The value did not equal the required comparison value.</summary>
    public const string NotEqual = "NOT_EQUAL";

    /// <summary>The value already exists.</summary>
    public const string Duplicate = "DUPLICATE";
}
