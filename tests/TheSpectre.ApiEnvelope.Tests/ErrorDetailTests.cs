using NUnit.Framework;
using TheSpectre.ApiEnvelope;

namespace TheSpectre.ApiEnvelope.Tests;

[TestFixture]
public sealed class ErrorDetailTests
{
    [Test]
    public void Constructor_WithAllSupportedParamValueTypes_Succeeds()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["text"] = "abc",
            ["int"] = 400,
            ["long"] = 9_000_000_000L,
            ["decimal"] = 12.34m,
            ["double"] = 1.5d,
            ["bool"] = true,
            ["nothing"] = null,
        };

        var detail = new ErrorDetail("title", "TOO_LONG", parameters);

        Assert.That(detail.Params, Is.Not.Null);
        Assert.That(detail.Params!.Count, Is.EqualTo(7));
    }

    [Test]
    public void Constructor_WithUnsupportedParamValueType_ThrowsNotSupportedException()
    {
        var parameters = new Dictionary<string, object?> { ["when"] = DateTimeOffset.UtcNow };

        var ex = Assert.Throws<NotSupportedException>(
            () => new ErrorDetail("title", "TOO_LONG", parameters));

        Assert.That(ex!.Message, Does.Contain("when"));
        Assert.That(ex.Message, Does.Contain("DateTimeOffset"));
    }

    [Test]
    public void Constructor_WithEmptyParams_NormalisesParamsToNull()
    {
        var detail = new ErrorDetail("title", "REQUIRED", new Dictionary<string, object?>());

        Assert.That(detail.Params, Is.Null);
    }

    [Test]
    public void Constructor_WithoutParams_LeavesParamsNull()
    {
        var detail = new ErrorDetail("title", "REQUIRED");

        Assert.That(detail.Field, Is.EqualTo("title"));
        Assert.That(detail.ErrorCode, Is.EqualTo("REQUIRED"));
        Assert.That(detail.Params, Is.Null);
    }

    [Test]
    public void Constructor_CopiesParams_SoLaterCallerMutationCannotEscapeValidation()
    {
        var source = new Dictionary<string, object?> { ["max"] = 400 };
        var detail = new ErrorDetail("title", "TOO_LONG", source);

        source["when"] = DateTimeOffset.UtcNow;

        Assert.That(detail.Params!.Count, Is.EqualTo(1));
        Assert.That(detail.Params.ContainsKey("when"), Is.False);
    }

    [Test]
    public void Constructor_WithWhitespaceField_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ErrorDetail("   ", "REQUIRED"));
    }

    [Test]
    public void Constructor_WithWhitespaceErrorCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ErrorDetail("title", "   "));
    }
}
