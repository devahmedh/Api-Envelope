using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class SuccessEnvelopeTests
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Sample(int Id, string Name);

    private static HttpContext CreateContext(string path = "/api/projects")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationId.ItemsKey] = TraceId;
        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEnd();
    }

    [Test]
    public void ShouldWrap_ForAnOrdinaryValue_IsTrue()
    {
        Assert.That(
            SuccessEnvelope.ShouldWrap(CreateContext(), new ApiEnvelopeOptions(), new Sample(1, "x")),
            Is.True);
    }

    [Test]
    public void ShouldWrap_ForNull_IsTrue()
    {
        // A 204 turned into 200 carries a null result — it must still be enveloped.
        Assert.That(
            SuccessEnvelope.ShouldWrap(CreateContext(), new ApiEnvelopeOptions(), null),
            Is.True);
    }

    [Test]
    public void ShouldWrap_ForAnAlreadyEnvelopedValue_IsFalse()
    {
        var alreadyWrapped = ApiResponse.Success(new Sample(1, "x"), TraceId);

        Assert.That(
            SuccessEnvelope.ShouldWrap(CreateContext(), new ApiEnvelopeOptions(), alreadyWrapped),
            Is.False);
    }

    [Test]
    public void ShouldWrap_OnAnExcludedPath_IsFalse()
    {
        Assert.That(
            SuccessEnvelope.ShouldWrap(CreateContext("/health"), new ApiEnvelopeOptions(), new Sample(1, "x")),
            Is.False);
    }

    [TestCase(200, 200)]
    [TestCase(201, 201)]
    [TestCase(202, 202)]
    [TestCase(204, 200)]
    public void NormaliseStatusCode_TurnsOnlyNoContentIntoOk(int input, int expected)
    {
        Assert.That(SuccessEnvelope.NormaliseStatusCode(input), Is.EqualTo(expected));
    }

    [Test]
    public async Task WriteAsync_ProducesTheEnvelopeWithTheRenamedResultSlot()
    {
        var context = CreateContext();

        await SuccessEnvelope.WriteAsync(
            context, new Sample(42, "Ahmed"), 200, new ApiEnvelopeOptions(), Json);

        Assert.That(context.Response.StatusCode, Is.EqualTo(200));
        Assert.That(context.Response.ContentType, Is.EqualTo("application/json; charset=utf-8"));
        Assert.That(ReadBody(context), Is.EqualTo(
            "{\"isSuccess\":true,\"statusCode\":200,\"result\":{\"id\":42,\"name\":\"Ahmed\"}," +
            $"\"errorCode\":null,\"correlationId\":\"{TraceId}\"}}"));
    }

    [Test]
    public async Task WriteAsync_ForNoContent_EmitsTwoHundredWithANullResult()
    {
        var context = CreateContext();

        await SuccessEnvelope.WriteAsync(context, null, 204, new ApiEnvelopeOptions(), Json);

        Assert.That(context.Response.StatusCode, Is.EqualTo(200));
        Assert.That(ReadBody(context), Is.EqualTo(
            "{\"isSuccess\":true,\"statusCode\":200,\"result\":null," +
            $"\"errorCode\":null,\"correlationId\":\"{TraceId}\"}}"));
    }

    [Test]
    public async Task WriteAsync_EchoesTheCorrelationIdHeader()
    {
        var context = CreateContext();

        await SuccessEnvelope.WriteAsync(context, new Sample(1, "x"), 200, new ApiEnvelopeOptions(), Json);

        Assert.That(context.Response.Headers["X-Correlation-Id"].ToString(), Is.EqualTo(TraceId));
    }

    [Test]
    public async Task WriteAsync_ForAPagedResult_NestsDataAndPaginationInsideResult()
    {
        var context = CreateContext();
        var paged = new PagedResult<Sample>(
            new[] { new Sample(3, "C") },
            new PaginationData(2, 1, 5));

        await SuccessEnvelope.WriteAsync(context, paged, 200, new ApiEnvelopeOptions(), Json);

        var body = ReadBody(context);

        Assert.That(body, Does.Contain("\"result\":{\"data\":[{\"id\":3,\"name\":\"C\"}]"));
        Assert.That(body, Does.Contain("\"pagination\":{\"currentPage\":2,\"pageSize\":1,\"rowCount\":5"));
    }
}
