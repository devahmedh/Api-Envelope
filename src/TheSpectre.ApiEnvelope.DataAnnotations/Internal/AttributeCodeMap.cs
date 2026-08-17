using System.ComponentModel.DataAnnotations;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Internal;

/// <summary>Maps a <see cref="ValidationAttribute"/> to a stable error key and its bounds.</summary>
/// <remarks>
/// The canonical <c>params</c> keys — <c>max</c>, <c>min</c>, <c>length</c>, <c>comparison</c> —
/// are fixed by the FluentValidation integration's own map. Both integrations must produce the
/// same keys or the client has two contracts, which is the single thing this library exists to
/// prevent. The parity suite enforces it.
/// </remarks>
internal static class AttributeCodeMap
{
    /// <summary>Attempts to map <paramref name="attribute"/> to a key and parameters.</summary>
    internal static bool TryMap(
        ValidationAttribute attribute,
        out string errorCode,
        out IReadOnlyDictionary<string, object?>? parameters)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        switch (attribute)
        {
            case RequiredAttribute:
                errorCode = ValidationErrorCodes.Required;
                parameters = null;
                return true;

            case StringLengthAttribute stringLength:
                errorCode = ValidationErrorCodes.TooLong;
                parameters = stringLength.MinimumLength > 0
                    ? Build(("max", stringLength.MaximumLength), ("min", stringLength.MinimumLength))
                    : Build(("max", stringLength.MaximumLength));
                return true;

            case MaxLengthAttribute maxLength:
                errorCode = ValidationErrorCodes.TooLong;
                parameters = Build(("max", maxLength.Length));
                return true;

            case MinLengthAttribute minLength:
                errorCode = ValidationErrorCodes.TooShort;
                parameters = Build(("min", minLength.Length));
                return true;

            case RangeAttribute range:
                errorCode = ValidationErrorCodes.OutOfRange;
                parameters = Build(("min", RangeBoundOrNull(range.Minimum)), ("max", RangeBoundOrNull(range.Maximum)));
                return true;

            case EmailAddressAttribute:
                errorCode = ValidationErrorCodes.InvalidEmail;
                parameters = null;
                return true;

            case UrlAttribute:
                errorCode = ValidationErrorCodes.InvalidUrl;
                parameters = null;
                return true;

            case RegularExpressionAttribute:
                errorCode = ValidationErrorCodes.InvalidFormat;
                parameters = null;
                return true;

            case CompareAttribute:
                errorCode = ValidationErrorCodes.NotEqual;
                parameters = null;
                return true;

            default:
                errorCode = string.Empty;
                parameters = null;
                return false;
        }
    }

    // Filters unsupported value types rather than letting ErrorDetail's constructor throw.
    // RangeAttribute's object bounds in particular can be a DateTime, which would otherwise
    // take down the response after the body may already have started.
    private static IReadOnlyDictionary<string, object?>? Build(
        params (string Key, object? Value)[] entries)
    {
        Dictionary<string, object?>? parameters = null;

        foreach (var (key, value) in entries)
        {
            if (!IsSupported(value))
            {
                continue;
            }

            parameters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            parameters[key] = value;
        }

        return parameters;
    }

    private static bool IsSupported(object? value) =>
        value is string or int or long or decimal or double or bool;

    // RangeAttribute's Type,string,string constructor (used for types like DateTime) keeps
    // Minimum/Maximum as the raw constructor strings until model validation first calls IsValid,
    // which lazily converts them in place — e.g. to an actual DateTime. A pre-conversion string
    // is not a meaningful bound, and a post-conversion DateTime is not a permitted ErrorDetail
    // params value, so only genuinely numeric/bool bounds (what Range's numeric constructors
    // naturally produce) are ever surfaced.
    private static object? RangeBoundOrNull(object? value) =>
        value is int or long or decimal or double or bool ? value : null;
}
