using System.Text.Json;
using FluentValidation.Results;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;
using TheSpectre.ApiEnvelope.FluentValidation.Internal;

namespace TheSpectre.ApiEnvelope.FluentValidation;

/// <summary>Maps FluentValidation failures onto the envelope's <c>details</c> shape.</summary>
public static class ValidationFailureExtensions
{
    /// <summary>Converts a single failure into an <see cref="ErrorDetail"/>.</summary>
    /// <param name="failure">The failure to convert.</param>
    /// <param name="namingPolicy">
    /// The naming policy used to turn the CLR property path into the JSON path the client sees.
    /// Pass the application's <see cref="JsonSerializerOptions.PropertyNamingPolicy"/>.
    /// </param>
    /// <remarks>
    /// <see cref="ValidationFailure.ErrorMessage"/> is deliberately ignored. It is an English
    /// sentence, and putting it on the wire is the exact failure this library exists to prevent.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static ErrorDetail ToErrorDetail(
        this ValidationFailure failure,
        JsonNamingPolicy? namingPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new ErrorDetail(
            JsonFieldPath.FromClrPath(failure.PropertyName, namingPolicy),
            failure.ErrorCode,
            BuildParams(failure));
    }

    /// <summary>Converts every failure in a result, preserving order.</summary>
    /// <param name="result">The validation result.</param>
    /// <param name="namingPolicy">The naming policy for JSON field paths.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static IReadOnlyList<ErrorDetail> ToErrorDetails(
        this ValidationResult result,
        JsonNamingPolicy? namingPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var details = new List<ErrorDetail>(result.Errors.Count);

        foreach (var failure in result.Errors)
        {
            details.Add(failure.ToErrorDetail(namingPolicy));
        }

        return details;
    }

    private static IReadOnlyDictionary<string, object?>? BuildParams(ValidationFailure failure)
    {
        var placeholders = failure.FormattedMessagePlaceholderValues;

        if (placeholders is null || placeholders.Count == 0)
        {
            return null;
        }

        Dictionary<string, object?>? parameters = null;

        foreach (var pair in placeholders)
        {
            if (ParamNameMap.IsDropped(pair.Key) || !IsSupported(pair.Value))
            {
                continue;
            }

            var canonicalKey = ParamNameMap.ToCanonical(pair.Key);

            // FluentValidation's MaximumLength(n) rule sets its MinLength placeholder to 0 —
            // there is no minimum. A zero minimum is the absence of a constraint, not a bound
            // worth putting on the wire, so a client rendering params.min would show "at least
            // 0 characters" for a rule that never expressed a minimum in the first place.
            if (canonicalKey == "min" && IsZero(pair.Value))
            {
                continue;
            }

            parameters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            parameters[canonicalKey] = pair.Value;
        }

        return parameters;
    }

    // Mirrors ErrorDetail's permitted set. Filtering here rather than letting the constructor
    // throw matters: a DateTimeOffset placeholder from a custom validator would otherwise take
    // down the response instead of simply not being rendered.
    private static bool IsSupported(object? value) =>
        value is null or string or int or long or decimal or double or bool;

    private static bool IsZero(object? value) => value switch
    {
        int i => i == 0,
        long l => l == 0,
        decimal d => d == 0,
        double d => d == 0,
        _ => false,
    };
}
