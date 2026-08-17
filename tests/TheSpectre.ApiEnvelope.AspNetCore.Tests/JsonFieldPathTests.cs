using System.Text.Json;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class JsonFieldPathTests
{
    private static readonly JsonNamingPolicy Camel = JsonNamingPolicy.CamelCase;

    [TestCase("Title", "title")]
    [TestCase("Address.City", "address.city")]
    [TestCase("Items[0].Quantity", "items[0].quantity")]
    [TestCase("Orders[12].Lines[3].UnitPrice", "orders[12].lines[3].unitPrice")]
    [TestCase("ID", "id")]
    [TestCase("", "")]
    public void FromClrPath_WithCamelCasePolicy_ProducesTheJsonPathTheClientSees(
        string clrPath, string expected)
    {
        Assert.That(JsonFieldPath.FromClrPath(clrPath, Camel), Is.EqualTo(expected));
    }

    [Test]
    public void FromClrPath_WithNoPolicy_LeavesSegmentNamesUnchanged()
    {
        Assert.That(JsonFieldPath.FromClrPath("Address.City", null), Is.EqualTo("Address.City"));
    }

    [Test]
    public void FromClrPath_PreservesIndexersExactly()
    {
        Assert.That(JsonFieldPath.FromClrPath("Items[0]", Camel), Is.EqualTo("items[0]"));
    }
}
