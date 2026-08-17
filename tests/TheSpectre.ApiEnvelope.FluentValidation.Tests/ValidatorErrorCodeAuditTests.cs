using System.Reflection;
using FluentValidation;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.FluentValidation;

namespace TheSpectre.ApiEnvelope.FluentValidation.Tests;

[TestFixture]
public sealed class ValidatorErrorCodeAuditTests
{
    internal sealed record Project(string Title, string Code);

    internal sealed class GoodValidator : AbstractValidator<Project>
    {
        public GoodValidator()
        {
            RuleFor(p => p.Title).NotEmpty().WithErrorCode(ValidationErrorCodes.Required);
            RuleFor(p => p.Code).MaximumLength(10).WithErrorCode(ValidationErrorCodes.TooLong);
        }
    }

    internal sealed class ForgetfulValidator : AbstractValidator<Project>
    {
        public ForgetfulValidator()
        {
            RuleFor(p => p.Title).NotEmpty();   // no WithErrorCode — this is the bug
        }
    }

    [Test]
    public void FindRulesWithoutExplicitErrorCodes_ForAValidatorThatDeclaresThemAll_ReportsNothing()
    {
        var findings = ValidatorErrorCodeAudit.FindRulesWithoutExplicitErrorCodes(
            typeof(GoodValidator).Assembly);

        Assert.That(findings.Any(f => f.Contains(nameof(GoodValidator), StringComparison.Ordinal)),
            Is.False);
    }

    [Test]
    public void FindRulesWithoutExplicitErrorCodes_ForAValidatorMissingOne_NamesTheValidatorAndProperty()
    {
        var findings = ValidatorErrorCodeAudit.FindRulesWithoutExplicitErrorCodes(
            typeof(ForgetfulValidator).Assembly);

        var finding = findings.SingleOrDefault(
            f => f.Contains(nameof(ForgetfulValidator), StringComparison.Ordinal));

        Assert.That(finding, Is.Not.Null);
        Assert.That(finding, Does.Contain("Title"));
    }

    [Test]
    public void FindRulesWithoutExplicitErrorCodes_WithNullAssembly_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ValidatorErrorCodeAudit.FindRulesWithoutExplicitErrorCodes(null!));
    }
}
