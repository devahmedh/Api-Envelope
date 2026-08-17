using System.Text.Json;
using FluentValidation.Results;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.FluentValidation;

namespace TheSpectre.ApiEnvelope.FluentValidation.Tests;

[TestFixture]
public sealed class ValidationFailureExtensionsTests
{
    private static readonly JsonNamingPolicy Camel = JsonNamingPolicy.CamelCase;

    private static ValidationFailure CreateFailure(
        string propertyName,
        string errorCode,
        Dictionary<string, object>? placeholders = null)
    {
        var failure = new ValidationFailure(propertyName, "an English sentence nobody should see")
        {
            ErrorCode = errorCode,
        };

        if (placeholders is not null)
        {
            failure.FormattedMessagePlaceholderValues = placeholders;
        }

        return failure;
    }

    [Test]
    public void ToErrorDetail_UsesTheDeclaredErrorCodeAndNeverTheEnglishMessage()
    {
        var detail = CreateFailure("Title", "TOO_LONG").ToErrorDetail(Camel);

        Assert.That(detail.ErrorCode, Is.EqualTo("TOO_LONG"));
        Assert.That(detail.Field, Is.EqualTo("title"));
    }

    [Test]
    public void ToErrorDetail_ConvertsNestedPropertyPathsToJsonPaths()
    {
        var detail = CreateFailure("Address.City", "REQUIRED").ToErrorDetail(Camel);

        Assert.That(detail.Field, Is.EqualTo("address.city"));
    }

    [Test]
    public void ToErrorDetail_MapsMaxLengthPlaceholderToTheCanonicalMaxParam()
    {
        var detail = CreateFailure("Title", "TOO_LONG", new Dictionary<string, object>
        {
            ["MaxLength"] = 400,
        }).ToErrorDetail(Camel);

        Assert.That(detail.Params, Is.Not.Null);
        Assert.That(detail.Params!["max"], Is.EqualTo(400));
    }

    [Test]
    public void ToErrorDetail_DropsFluentValidationInternalPlaceholders()
    {
        var detail = CreateFailure("Title", "TOO_LONG", new Dictionary<string, object>
        {
            ["PropertyName"] = "Title",
            ["PropertyValue"] = "a very long value",
            ["PropertyPath"] = "Title",
            ["MaxLength"] = 400,
        }).ToErrorDetail(Camel);

        Assert.That(detail.Params!.Count, Is.EqualTo(1));
        Assert.That(detail.Params.ContainsKey("propertyName"), Is.False);
        Assert.That(detail.Params.ContainsKey("propertyValue"), Is.False);
    }

    [Test]
    public void ToErrorDetail_DropsPlaceholderValuesOfUnsupportedTypes()
    {
        var detail = CreateFailure("When", "OUT_OF_RANGE", new Dictionary<string, object>
        {
            ["ComparisonValue"] = DateTimeOffset.UtcNow,
        }).ToErrorDetail(Camel);

        // ErrorDetail's constructor would throw on an unsupported type, so the mapper must
        // filter rather than pass it through and crash mid-response.
        Assert.That(detail.Params, Is.Null);
    }

    [Test]
    public void ToErrorDetail_WithNoPlaceholders_LeavesParamsNull()
    {
        Assert.That(CreateFailure("Code", "REQUIRED").ToErrorDetail(Camel).Params, Is.Null);
    }

    [Test]
    public void ToErrorDetails_MapsEveryFailureInOrder()
    {
        var result = new ValidationResult(new[]
        {
            CreateFailure("Title", "TOO_LONG", new Dictionary<string, object> { ["MaxLength"] = 400 }),
            CreateFailure("Code", "REQUIRED"),
        });

        var details = result.ToErrorDetails(Camel);

        Assert.That(details, Has.Count.EqualTo(2));
        Assert.That(details[0].Field, Is.EqualTo("title"));
        Assert.That(details[1].Field, Is.EqualTo("code"));
    }
}
