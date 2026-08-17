using System.Reflection;
using FluentValidation;

namespace TheSpectre.ApiEnvelope.FluentValidation;

/// <summary>Finds validator rules that never declared an explicit error key.</summary>
/// <remarks>
/// Intended for a single NUnit test in each consuming project:
/// <code>
/// [Test]
/// public void AllValidators_DeclareExplicitErrorCodes()
/// {
///     var findings = ValidatorErrorCodeAudit.FindRulesWithoutExplicitErrorCodes(
///         typeof(SomeValidator).Assembly);
///     Assert.That(findings, Is.Empty);
/// }
/// </code>
/// A rule without <c>WithErrorCode</c> ships FluentValidation's default name — for example
/// <c>NotEmptyValidator</c> — which no client has a translation for, so the user sees nothing.
/// This turns that from a convention into a build failure.
/// </remarks>
public static class ValidatorErrorCodeAudit
{
    /// <summary>Returns one description per rule component lacking an explicit error key.</summary>
    /// <param name="assembly">The assembly to scan for <see cref="IValidator"/> implementations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is null.</exception>
    public static IReadOnlyList<string> FindRulesWithoutExplicitErrorCodes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var findings = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !typeof(IValidator).IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            if (Activator.CreateInstance(type) is not IValidator validator)
            {
                continue;
            }

            foreach (var rule in validator.CreateDescriptor().Rules)
            {
                foreach (var component in rule.Components)
                {
                    // RuleComponent.ErrorCode is NULL until .WithErrorCode(...) is called —
                    // verified against FluentValidation 12.1.1. The "NotEmptyValidator"-style
                    // default only materialises later, on ValidationFailure.ErrorCode, as
                    // component.ErrorCode ?? component.Validator.Name. Checking ErrorCode's
                    // text alone would therefore never fire, and this guard would ship as a
                    // permanent no-op.
                    var effectiveCode = component.ErrorCode ?? component.Validator.Name;

                    if (IsFluentValidationDefault(effectiveCode))
                    {
                        findings.Add(
                            $"{type.FullName}.{rule.PropertyName} uses the default error code " +
                            $"'{effectiveCode}'. Add .WithErrorCode(...) with a stable key.");
                    }
                }
            }
        }

        return findings;
    }

    // FluentValidation's built-in codes are the validator type's name: NotEmptyValidator,
    // MaximumLengthValidator, EmailValidator, and so on. An explicit key is SCREAMING_SNAKE_CASE
    // by our own convention, so the two are unambiguous.
    private static bool IsFluentValidationDefault(string? errorCode) =>
        errorCode is not null && errorCode.EndsWith("Validator", StringComparison.Ordinal);
}
