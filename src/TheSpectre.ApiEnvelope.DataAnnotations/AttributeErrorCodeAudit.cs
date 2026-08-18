using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using TheSpectre.ApiEnvelope.DataAnnotations.Internal;

namespace TheSpectre.ApiEnvelope.DataAnnotations;

/// <summary>Finds validation attributes whose <c>ErrorMessage</c> is prose rather than a key.</summary>
/// <remarks>
/// DataAnnotations has no slot for an error key, so the key travels in <c>ErrorMessage</c>. Only
/// a <c>SCREAMING_SNAKE_CASE</c> value is treated as one; anything else is read as an English
/// sentence and discarded in favour of a key inferred from the failing attribute. That is the
/// right default — server-authored prose must never reach a client — but it is silent, and a
/// <c>PascalCase</c> key looks close enough to correct to survive review. The response still
/// carries a plausible key, just not the one the model author wrote, so the mismatch only
/// surfaces when a translation lookup quietly misses in the running client.
/// <para>
/// Intended for a single NUnit test in each consuming project:
/// </para>
/// <code>
/// [Test]
/// public void AllValidationAttributes_UseKeysNotProse()
/// {
///     var findings = AttributeErrorCodeAudit.FindAttributesWithProseErrorMessages(
///         typeof(CreateProject).Assembly);
///
///     Assert.That(findings, Is.Empty);
/// }
/// </code>
/// <para>
/// The FluentValidation package's <c>ValidatorErrorCodeAudit</c> is the same guard for the other
/// integration. Note that the two libraries carry the key in different places: FluentValidation
/// has a real <c>ErrorCode</c> slot, set with <c>.WithErrorCode(...)</c>, and its message is
/// ignored entirely — so this pattern rule does not apply there, and a key of any shape survives.
/// </para>
/// </remarks>
public static class AttributeErrorCodeAudit
{
    /// <summary>Returns one description per attribute carrying prose instead of a key.</summary>
    /// <param name="assembly">The assembly to scan for models with validation attributes.</param>
    /// <remarks>
    /// An attribute with no <c>ErrorMessage</c> at all is not reported: the mapper infers the
    /// key from the attribute type, which is the documented behaviour for the built-in
    /// attributes and produces a correct key.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is null.</exception>
    [RequiresUnreferencedCode(
        "Scans every type and property in the assembly by reflection, which the trimmer cannot " +
        "root. Intended for a test project, which is never trimmed.")]
    public static IReadOnlyList<string> FindAttributesWithProseErrorMessages(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var findings = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            // Interfaces carry no instances to validate; compiler-generated closures and
            // iterator state machines carry no author-written attributes.
            if (type.IsInterface || type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>(true))
                {
                    var message = attribute.ErrorMessage;

                    if (message is null || ErrorCodeKey.IsKey(message))
                    {
                        continue;
                    }

                    findings.Add(
                        $"{type.FullName}.{property.Name} has " +
                        $"[{attribute.GetType().Name}(ErrorMessage = \"{message}\")]. " +
                        "Only SCREAMING_SNAKE_CASE is treated as a key; this value is discarded " +
                        "and a key inferred from the attribute is sent instead.");
                }
            }
        }

        return findings;
    }
}
