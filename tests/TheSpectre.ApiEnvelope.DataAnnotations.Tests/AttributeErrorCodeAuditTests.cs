using System.ComponentModel.DataAnnotations;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Tests;

/// <summary>Models the audit scans. Deliberately mixes correct and incorrect declarations.</summary>
public sealed class AuditedModel
{
    /// <summary>SCREAMING_SNAKE_CASE — preserved verbatim.</summary>
    [Required(ErrorMessage = ValidationErrorCodes.Required)]
    public string? Code { get; set; }

    /// <summary>PascalCase — looks like a key, is discarded as prose.</summary>
    [Required(ErrorMessage = "EmailInvalid")]
    public string? Email { get; set; }

    /// <summary>An actual sentence — discarded, which is the whole point of the rule.</summary>
    [StringLength(400, ErrorMessage = "Title must be at most 400 characters.")]
    public string? Title { get; set; }

    /// <summary>No ErrorMessage: the mapper infers TOO_LONG from the attribute. Not a finding.</summary>
    [StringLength(50)]
    public string? Summary { get; set; }
}

/// <summary>
/// Adoption report F-3: the key pattern is load-bearing and was previously discoverable only by
/// reading the source.
/// </summary>
[TestFixture]
public sealed class AttributeErrorCodeAuditTests
{
    [Test]
    public void Audit_ReportsAPascalCaseKey()
    {
        Assert.That(
            Findings(),
            Has.One.Contains("Email").And.One.Contains("EmailInvalid"));
    }

    [Test]
    public void Audit_ReportsAnEnglishSentence()
    {
        Assert.That(Findings(), Has.One.Contains("Title"));
    }

    [Test]
    public void Audit_DoesNotReportAScreamingSnakeCaseKey()
    {
        Assert.That(Findings().Where(f => f.Contains(".Code")), Is.Empty);
    }

    /// <summary>
    /// An absent ErrorMessage is not a defect: the mapper infers the key from the attribute
    /// type. Reporting it would bury the two real findings in noise from every correct model.
    /// </summary>
    [Test]
    public void Audit_DoesNotReportAnAttributeWithNoErrorMessage()
    {
        Assert.That(Findings().Where(f => f.Contains(".Summary")), Is.Empty);
    }

    [Test]
    public void Audit_ExplainsWhatHappensToTheDiscardedValue()
    {
        Assert.That(
            Findings(),
            Has.All.Contains("Only SCREAMING_SNAKE_CASE is treated as a key"));
    }

    private static IEnumerable<string> Findings() =>
        AttributeErrorCodeAudit
            .FindAttributesWithProseErrorMessages(typeof(AuditedModel).Assembly)
            .Where(finding => finding.Contains(nameof(AuditedModel), StringComparison.Ordinal));
}
