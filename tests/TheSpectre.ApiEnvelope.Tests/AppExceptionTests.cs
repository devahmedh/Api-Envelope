using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.Tests;

[TestFixture]
public sealed class AppExceptionTests
{
    [Test]
    public void Constructor_WithDefaults_UsesStatusCode400()
    {
        var exception = new AppException("PROJECT_CODE_TAKEN");

        Assert.That(exception.ErrorCode, Is.EqualTo("PROJECT_CODE_TAKEN"));
        Assert.That(exception.StatusCode, Is.EqualTo(400));
        Assert.That(exception.Details, Is.Null);
    }

    [Test]
    public void Constructor_PreservesInnerException()
    {
        var inner = new InvalidOperationException("concurrency conflict");

        var exception = new AppException("CONFLICT", "diagnostic", 409, inner);

        Assert.That(exception.InnerException, Is.SameAs(inner));
        Assert.That(exception.StatusCode, Is.EqualTo(409));
        Assert.That(exception.Message, Is.EqualTo("diagnostic"));
    }

    [Test]
    public void Constructor_WithDetails_ExposesThem()
    {
        var details = new[] { new ErrorDetail("title", "REQUIRED") };

        var exception = new AppException("VALIDATION_FAILED", 400, details);

        Assert.That(exception.Details, Is.EqualTo(details));
        Assert.That(exception.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public void Constructor_WithWhitespaceErrorCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AppException("   "));
    }

    [Test]
    public void AppException_IsNotSealed_SoDomainSubclassesArePossible()
    {
        Assert.That(typeof(AppException).IsSealed, Is.False);
    }

    [Test]
    public void ErrorCodes_AreScreamingSnakeCase()
    {
        Assert.That(ErrorCodes.InternalError, Is.EqualTo("INTERNAL_ERROR"));
        Assert.That(ErrorCodes.ValidationFailed, Is.EqualTo("VALIDATION_FAILED"));
        Assert.That(ErrorCodes.NotFound, Is.EqualTo("NOT_FOUND"));
    }

    [Test]
    public void ValidationErrorCodes_AreScreamingSnakeCase()
    {
        Assert.That(ValidationErrorCodes.Required, Is.EqualTo("REQUIRED"));
        Assert.That(ValidationErrorCodes.TooLong, Is.EqualTo("TOO_LONG"));
        Assert.That(ValidationErrorCodes.OutOfRange, Is.EqualTo("OUT_OF_RANGE"));
    }
}
