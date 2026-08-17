namespace TheSpectre.ApiEnvelope.FluentValidation.Internal;

/// <summary>Maps FluentValidation placeholder names to canonical <c>params</c> keys.</summary>
/// <remarks>
/// Two things happen here. FluentValidation's own bookkeeping placeholders are dropped — they
/// carry the property name and the rejected value, which is user input and has no business on
/// the wire. And the bound placeholders are renamed to short canonical keys, so a client
/// renders "الحد الأقصى 400 حرف" from <c>params.max</c> regardless of which validation library
/// produced the failure. The DataAnnotations integration maps to these same keys.
/// </remarks>
internal static class ParamNameMap
{
    private static readonly HashSet<string> Dropped = new(StringComparer.Ordinal)
    {
        "PropertyName",
        "PropertyValue",
        "PropertyPath",
        "CollectionIndex",

        // TotalLength is the length of what the user actually typed — an observation about the
        // input, not a bound the client renders from. It belongs with PropertyValue.
        "TotalLength",
    };

    /// <summary>Whether <paramref name="placeholder"/> must not reach the wire.</summary>
    internal static bool IsDropped(string placeholder) => Dropped.Contains(placeholder);

    /// <summary>Returns the canonical <c>params</c> key for <paramref name="placeholder"/>.</summary>
    internal static string ToCanonical(string placeholder) => placeholder switch
    {
        "MaxLength" => "max",
        "MinLength" => "min",
        "ExactLength" => "length",
        "From" => "min",
        "To" => "max",
        "ComparisonValue" => "comparison",
        _ => char.ToLowerInvariant(placeholder[0]) + placeholder[1..],
    };
}
