using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Internal;

/// <summary>Decides whether an <c>ErrorMessage</c> is a key or an English sentence.</summary>
/// <remarks>
/// DataAnnotations has no slot for an error key, so the key travels in <c>ErrorMessage</c> — the
/// same string property that normally holds prose. Something has to tell the two apart, and the
/// shape of the string is all there is to go on. <c>SCREAMING_SNAKE_CASE</c> is the library's
/// documented key convention and no English sentence matches it, so a match is treated as a
/// deliberate key and passed through verbatim; anything else is discarded in favour of a key
/// inferred from the failing attribute, because putting server-authored prose on the wire is the
/// exact failure this library exists to prevent.
/// <para>
/// One definition, used by both the runtime mapper and
/// <see cref="AttributeErrorCodeAudit"/>, so an audit can never disagree with the mapper it is
/// auditing.
/// </para>
/// </remarks>
internal static partial class ErrorCodeKey
{
    /// <summary>Whether <paramref name="errorMessage"/> is shaped like a key.</summary>
    /// <param name="errorMessage">The attribute's <c>ErrorMessage</c>.</param>
    internal static bool IsKey([NotNullWhen(true)] string? errorMessage) =>
        errorMessage is not null && KeyPattern().IsMatch(errorMessage);

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
