using System.Reflection;
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
    public void Constructor_WithDetails_PreservesInnerException()
    {
        var inner = new InvalidOperationException("concurrency conflict");
        var details = new[] { new ErrorDetail("title", "REQUIRED") };

        var exception = new AppException("VALIDATION_FAILED", 400, details, "diagnostic", inner);

        Assert.That(exception.InnerException, Is.SameAs(inner));
    }

    [Test]
    public void Constructor_WithNullDetails_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AppException("VALIDATION_FAILED", 400, null!));
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
    public void ErrorCodes_EveryConstantIsScreamingSnakeCase()
    {
        AssertEveryConstantIsScreamingSnakeCase(typeof(ErrorCodes), expectedCount: 13);
    }

    [Test]
    public void ValidationErrorCodes_EveryConstantIsScreamingSnakeCase()
    {
        AssertEveryConstantIsScreamingSnakeCase(typeof(ValidationErrorCodes), expectedCount: 9);
    }

    private static void AssertEveryConstantIsScreamingSnakeCase(Type type, int expectedCount)
    {
        var constants = type
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false })
            .ToArray();

        Assert.That(
            constants.Length,
            Is.EqualTo(expectedCount),
            $"{type.Name} should declare exactly {expectedCount} constants.");

        foreach (var constant in constants)
        {
            var value = (string)constant.GetRawConstantValue()!;

            Assert.That(
                value,
                Does.Match("^[A-Z][A-Z0-9_]*$"),
                $"{type.Name}.{constant.Name} is not SCREAMING_SNAKE_CASE.");
        }
    }
}
