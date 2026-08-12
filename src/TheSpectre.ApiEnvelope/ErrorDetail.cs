namespace TheSpectre.ApiEnvelope;

/// <summary>
/// A single field-level failure carried in the <c>details</c> array of an
/// <c>ApiResponse&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// <see cref="ErrorCode"/> is a stable key the client switches on; <see cref="Params"/>
/// carries the structured values the client substitutes into its own translated string,
/// so the server never composes user-facing text.
/// </remarks>
public sealed class ErrorDetail
{
    /// <summary>Initialises a new <see cref="ErrorDetail"/>.</summary>
    /// <param name="field">
    /// The JSON property path of the failing field as the client sees it, for example
    /// <c>title</c>, <c>address.city</c> or <c>items[0].quantity</c>.
    /// </param>
    /// <param name="errorCode">The stable SCREAMING_SNAKE_CASE key for this failure.</param>
    /// <param name="parameters">
    /// Optional structured values for client-side message rendering. Permitted value types
    /// are <see cref="string"/>, <see cref="int"/>, <see cref="long"/>,
    /// <see cref="decimal"/>, <see cref="double"/>, <see cref="bool"/> and <see langword="null"/>.
    /// An empty dictionary is normalised to <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="field"/> or <paramref name="errorCode"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="parameters"/> contains a value of an unsupported type.
    /// </exception>
    public ErrorDetail(
        string field,
        string errorCode,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        Field = field;
        ErrorCode = errorCode;
        Params = Normalise(parameters);
    }

    /// <summary>The JSON property path of the failing field.</summary>
    public string Field { get; }

    /// <summary>The stable key for this failure.</summary>
    public string ErrorCode { get; }

    /// <summary>Structured values for client-side rendering, or <see langword="null"/>.</summary>
    public IReadOnlyDictionary<string, object?>? Params { get; }

    /// <summary>
    /// Whether <paramref name="value"/> is a permitted <c>params</c> value type.
    /// </summary>
    private static bool IsSupportedParamValue(object? value) =>
        value is null or string or int or long or decimal or double or bool;

    private static IReadOnlyDictionary<string, object?>? Normalise(
        IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        foreach (var pair in parameters)
        {
            if (!IsSupportedParamValue(pair.Value))
            {
                throw new NotSupportedException(
                    $"Parameter '{pair.Key}' has unsupported type '{pair.Value!.GetType().Name}'. " +
                    "Supported params value types are: string, int, long, decimal, double, bool, null.");
            }
        }

        // Copy defensively. Returning the caller's instance would let them add an
        // unsupported value after construction, reintroducing the very failure the
        // constructor-time check exists to prevent.
        return new Dictionary<string, object?>(parameters);
    }
}
