using System.Diagnostics;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class CorrelationIdTests
{
    [TestCase("4bf92f3577b34da6a3ce929d0e0e4736")]
    [TestCase("abc-123_x.y")]
    [TestCase("A")]
    public void IsWellFormed_ForPermittedCharacters_ReturnsTrue(string value)
    {
        Assert.That(CorrelationId.IsWellFormed(value), Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("has space")]
    [TestCase("has\nnewline")]
    [TestCase("has\r\ninjection")]
    [TestCase("semi;colon")]
    public void IsWellFormed_ForDisallowedInput_ReturnsFalse(string? value)
    {
        Assert.That(CorrelationId.IsWellFormed(value), Is.False);
    }

    [Test]
    public void IsWellFormed_ForValueLongerThan128Characters_ReturnsFalse()
    {
        Assert.That(CorrelationId.IsWellFormed(new string('a', 129)), Is.False);
        Assert.That(CorrelationId.IsWellFormed(new string('a', 128)), Is.True);
    }

    [Test]
    public void Resolve_WithWellFormedInboundValue_EchoesIt()
    {
        Assert.That(CorrelationId.Resolve("abc-123"), Is.EqualTo("abc-123"));
    }

    [Test]
    public void Resolve_WithMalformedInboundValue_GeneratesInsteadOfEchoing()
    {
        var resolved = CorrelationId.Resolve("bad value\r\nInjected: header");

        Assert.That(resolved, Does.Not.Contain("Injected"));
        Assert.That(CorrelationId.IsWellFormed(resolved), Is.True);
    }

    [Test]
    public void Resolve_WithNoInboundValueAndAnActivity_UsesTheActivityTraceId()
    {
        using var activity = new Activity("test");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        var resolved = CorrelationId.Resolve(null);

        Assert.That(resolved, Is.EqualTo(activity.TraceId.ToString()));
        Assert.That(resolved, Has.Length.EqualTo(32));
    }

    [Test]
    public void Resolve_WithNoInboundValueAndNoActivity_GeneratesAWellFormedId()
    {
        Activity.Current = null;

        var resolved = CorrelationId.Resolve(null);

        Assert.That(CorrelationId.IsWellFormed(resolved), Is.True);
        Assert.That(resolved, Has.Length.EqualTo(32));
    }
}
