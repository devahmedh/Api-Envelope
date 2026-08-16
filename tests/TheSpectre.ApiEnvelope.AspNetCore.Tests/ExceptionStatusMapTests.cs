using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class ExceptionStatusMapTests
{
    private static ApiEnvelopeOptions Options => new();

    [Test]
    public void Resolve_ForAppException_UsesItsOwnStatusAndErrorCode()
    {
        var exception = new AppException("PROJECT_CODE_TAKEN", statusCode: 409);

        var (status, errorCode) = ExceptionStatusMap.Resolve(exception, Options);

        Assert.That(status, Is.EqualTo(409));
        Assert.That(errorCode, Is.EqualTo("PROJECT_CODE_TAKEN"));
    }

    [Test]
    public void Resolve_ForAppExceptionSubclass_StillUsesItsOwnStatusAndErrorCode()
    {
        var exception = new DerivedAppException();

        var (status, errorCode) = ExceptionStatusMap.Resolve(exception, Options);

        Assert.That(status, Is.EqualTo(418));
        Assert.That(errorCode, Is.EqualTo("DERIVED"));
    }

    [Test]
    public void Resolve_ForMappedType_UsesTheMappedStatus()
    {
        var (status, errorCode) = ExceptionStatusMap.Resolve(new TimeoutException(), Options);

        Assert.That(status, Is.EqualTo(StatusCodes.Status504GatewayTimeout));
        Assert.That(errorCode, Is.EqualTo(ErrorCodes.Timeout));
    }

    [Test]
    public void Resolve_ForSubclassOfMappedType_WalksTheHierarchy()
    {
        var (status, errorCode) = ExceptionStatusMap.Resolve(new ArgumentNullException("x"), Options);

        Assert.That(status, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(errorCode, Is.EqualTo(ErrorCodes.BadRequest));
    }

    [Test]
    public void Resolve_ForBadHttpRequestException_UsesItsOwnStatus()
    {
        var exception = new BadHttpRequestException("malformed", StatusCodes.Status413PayloadTooLarge);

        var (status, errorCode) = ExceptionStatusMap.Resolve(exception, Options);

        Assert.That(status, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
        Assert.That(errorCode, Is.EqualTo(ErrorCodes.BadRequest));
    }

    [Test]
    public void Resolve_ForUnrecognisedException_IsFiveHundredInternalError()
    {
        var (status, errorCode) = ExceptionStatusMap.Resolve(new InvalidOperationException(), Options);

        Assert.That(status, Is.EqualTo(StatusCodes.Status500InternalServerError));
        Assert.That(errorCode, Is.EqualTo(ErrorCodes.InternalError));
    }

    [Test]
    public void Resolve_WhenConsumerRemovesADefault_FallsBackToFiveHundred()
    {
        var options = new ApiEnvelopeOptions();
        options.ExceptionStatusCodes.Remove(typeof(KeyNotFoundException));

        var (status, _) = ExceptionStatusMap.Resolve(new KeyNotFoundException(), options);

        Assert.That(status, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    [Test]
    public void Resolve_PrefersTheMostDerivedMapping()
    {
        var options = new ApiEnvelopeOptions();
        options.ExceptionStatusCodes[typeof(ArgumentNullException)] = StatusCodes.Status422UnprocessableEntity;

        var (status, _) = ExceptionStatusMap.Resolve(new ArgumentNullException("x"), options);

        Assert.That(status, Is.EqualTo(StatusCodes.Status422UnprocessableEntity));
    }

    private sealed class DerivedAppException()
        : AppException("DERIVED", statusCode: 418);
}
