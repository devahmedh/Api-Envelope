using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class ApiEnvelopeOptionsTests
{
    [Test]
    public void Defaults_ExcludeTheFourInfrastructurePathPrefixes()
    {
        var options = new ApiEnvelopeOptions();

        Assert.That(options.ExcludedPathPrefixes, Is.EquivalentTo(new[]
        {
            new PathString("/swagger"),
            new PathString("/health"),
            new PathString("/metrics"),
            new PathString("/hubs"),
        }));
    }

    [Test]
    public void Defaults_HideMessageOutsideDevelopment()
    {
        var options = new ApiEnvelopeOptions();

        Assert.That(options.MessageVisibility, Is.EqualTo(MessageVisibility.DevelopmentOnly));
    }

    [Test]
    public void Defaults_UseXCorrelationIdHeader()
    {
        var options = new ApiEnvelopeOptions();

        Assert.That(options.CorrelationIdHeaderName, Is.EqualTo("X-Correlation-Id"));
    }

    [Test]
    public void Defaults_MapTheDocumentedExceptionTypes()
    {
        var options = new ApiEnvelopeOptions();

        Assert.That(options.ExceptionStatusCodes[typeof(TimeoutException)], Is.EqualTo(504));
        Assert.That(options.ExceptionStatusCodes[typeof(NotImplementedException)], Is.EqualTo(501));
        Assert.That(options.ExceptionStatusCodes[typeof(KeyNotFoundException)], Is.EqualTo(404));
        Assert.That(options.ExceptionStatusCodes[typeof(ArgumentException)], Is.EqualTo(400));
    }

    [Test]
    public void ExceptionStatusCodes_IsMutableSoConsumersCanRemoveADefault()
    {
        var options = new ApiEnvelopeOptions();

        var removed = options.ExceptionStatusCodes.Remove(typeof(KeyNotFoundException));

        Assert.That(removed, Is.True);
        Assert.That(options.ExceptionStatusCodes.ContainsKey(typeof(KeyNotFoundException)), Is.False);
    }

    [Test]
    public void NoEnvelopeAttribute_IsValidOnClassesAndMethodsAndIsInherited()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(NoEnvelopeAttribute), typeof(AttributeUsageAttribute))!;

        Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Class | AttributeTargets.Method));
        Assert.That(usage.Inherited, Is.True);
    }
}
