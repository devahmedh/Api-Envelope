using System.ComponentModel.DataAnnotations;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.DataAnnotations.Internal;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Tests;

[TestFixture]
public sealed class AttributeCodeMapTests
{
    [Test]
    public void TryMap_ForRequired_YieldsRequiredWithNoParams()
    {
        var mapped = AttributeCodeMap.TryMap(new RequiredAttribute(), out var code, out var parameters);

        Assert.That(mapped, Is.True);
        Assert.That(code, Is.EqualTo(ValidationErrorCodes.Required));
        Assert.That(parameters, Is.Null);
    }

    [Test]
    public void TryMap_ForStringLength_YieldsTooLongWithTheCanonicalMaxParam()
    {
        var mapped = AttributeCodeMap.TryMap(new StringLengthAttribute(400), out var code, out var parameters);

        Assert.That(mapped, Is.True);
        Assert.That(code, Is.EqualTo(ValidationErrorCodes.TooLong));
        Assert.That(parameters!["max"], Is.EqualTo(400));
    }

    [Test]
    public void TryMap_ForStringLengthWithAMinimum_CarriesBothBounds()
    {
        var attribute = new StringLengthAttribute(400) { MinimumLength = 3 };

        AttributeCodeMap.TryMap(attribute, out _, out var parameters);

        Assert.That(parameters!["max"], Is.EqualTo(400));
        Assert.That(parameters["min"], Is.EqualTo(3));
    }

    [Test]
    public void TryMap_ForMaxLength_YieldsTooLongWithMax()
    {
        AttributeCodeMap.TryMap(new MaxLengthAttribute(50), out var code, out var parameters);

        Assert.That(code, Is.EqualTo(ValidationErrorCodes.TooLong));
        Assert.That(parameters!["max"], Is.EqualTo(50));
    }

    [Test]
    public void TryMap_ForMinLength_YieldsTooShortWithMin()
    {
        AttributeCodeMap.TryMap(new MinLengthAttribute(2), out var code, out var parameters);

        Assert.That(code, Is.EqualTo(ValidationErrorCodes.TooShort));
        Assert.That(parameters!["min"], Is.EqualTo(2));
    }

    [Test]
    public void TryMap_ForRange_YieldsOutOfRangeWithBothBounds()
    {
        AttributeCodeMap.TryMap(new System.ComponentModel.DataAnnotations.RangeAttribute(1, 10), out var code, out var parameters);

        Assert.That(code, Is.EqualTo(ValidationErrorCodes.OutOfRange));
        Assert.That(parameters!["min"], Is.EqualTo(1));
        Assert.That(parameters["max"], Is.EqualTo(10));
    }

    [Test]
    public void TryMap_ForRangeWithUnsupportedBoundTypes_DropsTheParamsRatherThanThrowing()
    {
        // RangeAttribute(Type, string, string) yields object bounds that may not be a permitted
        // params value type. ErrorDetail's constructor would throw mid-response, so the map
        // must filter instead.
        var attribute = new System.ComponentModel.DataAnnotations.RangeAttribute(typeof(DateTime), "2020-01-01", "2030-01-01");

        var mapped = AttributeCodeMap.TryMap(attribute, out var code, out var parameters);

        Assert.That(mapped, Is.True);
        Assert.That(code, Is.EqualTo(ValidationErrorCodes.OutOfRange));
        Assert.That(parameters, Is.Null);
    }

    [Test]
    public void TryMap_ForRangeWithLongBounds_DropsTheParamsBecauseLongIsNoLongerSupported()
    {
        // The Type,string,string constructor keeps Minimum/Maximum as raw strings until the
        // first call to IsValid lazily converts them to the operand type — here, an actual long.
        var attribute = new System.ComponentModel.DataAnnotations.RangeAttribute(typeof(long), "10", "100");
        attribute.IsValid(50L);

        var mapped = AttributeCodeMap.TryMap(attribute, out var code, out var parameters);

        Assert.That(mapped, Is.True);
        Assert.That(code, Is.EqualTo(ValidationErrorCodes.OutOfRange));
        // long cannot be represented exactly by JavaScript's number type, so ErrorDetail's
        // constructor now throws on it. The map must filter instead of passing it through, or a
        // Range(long) bound would crash the response mid-write.
        Assert.That(parameters, Is.Null);
    }

    [Test]
    public void TryMap_ForRangeWithDecimalBounds_DropsTheParamsBecauseDecimalIsNoLongerSupported()
    {
        var attribute = new System.ComponentModel.DataAnnotations.RangeAttribute(typeof(decimal), "1.50", "99.99");
        attribute.IsValid(50m);

        var mapped = AttributeCodeMap.TryMap(attribute, out var code, out var parameters);

        Assert.That(mapped, Is.True);
        Assert.That(code, Is.EqualTo(ValidationErrorCodes.OutOfRange));
        Assert.That(parameters, Is.Null);
    }

    [Test]
    public void TryMap_ForEmailAddress_YieldsInvalidEmail()
    {
        AttributeCodeMap.TryMap(new EmailAddressAttribute(), out var code, out _);

        Assert.That(code, Is.EqualTo(ValidationErrorCodes.InvalidEmail));
    }

    [Test]
    public void TryMap_ForUrl_YieldsInvalidUrl()
    {
        AttributeCodeMap.TryMap(new UrlAttribute(), out var code, out _);

        Assert.That(code, Is.EqualTo(ValidationErrorCodes.InvalidUrl));
    }

    [Test]
    public void TryMap_ForRegularExpression_YieldsInvalidFormat()
    {
        AttributeCodeMap.TryMap(new RegularExpressionAttribute("^a+$"), out var code, out _);

        Assert.That(code, Is.EqualTo(ValidationErrorCodes.InvalidFormat));
    }

    [Test]
    public void TryMap_ForCompare_YieldsNotEqual()
    {
        AttributeCodeMap.TryMap(new CompareAttribute("Other"), out var code, out _);

        Assert.That(code, Is.EqualTo(ValidationErrorCodes.NotEqual));
    }

    [Test]
    public void TryMap_ForAnUnrecognisedAttribute_ReturnsFalse()
    {
        var mapped = AttributeCodeMap.TryMap(new CustomAttribute(), out var code, out var parameters);

        Assert.That(mapped, Is.False);
        Assert.That(code, Is.Empty);
        Assert.That(parameters, Is.Null);
    }

    private sealed class CustomAttribute : ValidationAttribute;
}
