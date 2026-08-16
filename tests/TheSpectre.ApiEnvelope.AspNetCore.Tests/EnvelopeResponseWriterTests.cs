using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class EnvelopeResponseWriterTests
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static HttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
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
    public async Task WriteAsync_SetsStatusCodeContentTypeAndEnvelopeBody()
    {
        var context = CreateContext();

        await EnvelopeResponseWriter.WriteAsync(
            context, 409, "PROJECT_CODE_TAKEN", "diagnostic", null, Json, CorrelationIdHeaderName);

        Assert.That(context.Response.StatusCode, Is.EqualTo(409));
        Assert.That(context.Response.ContentType, Is.EqualTo("application/json; charset=utf-8"));
        Assert.That(ReadBody(context), Is.EqualTo(
            "{\"isSuccess\":false,\"statusCode\":409,\"data\":null," +
            "\"errorCode\":\"PROJECT_CODE_TAKEN\",\"message\":\"diagnostic\"," +
            $"\"correlationId\":\"{TraceId}\"}}"));
    }

    [Test]
    public async Task WriteAsync_WithNullMessage_OmitsTheMessageProperty()
    {
        var context = CreateContext();

        await EnvelopeResponseWriter.WriteAsync(
            context, 500, ErrorCodes.InternalError, null, null, Json, CorrelationIdHeaderName);

        Assert.That(ReadBody(context), Does.Not.Contain("\"message\""));
    }

    [Test]
    public async Task WriteAsync_WithDetails_IncludesThem()
    {
        var context = CreateContext();
        var details = new[] { new ErrorDetail("title", ValidationErrorCodes.Required) };

        await EnvelopeResponseWriter.WriteAsync(
            context, 400, ErrorCodes.ValidationFailed, null, details, Json, CorrelationIdHeaderName);

        Assert.That(ReadBody(context), Does.Contain(
            "\"details\":[{\"field\":\"title\",\"errorCode\":\"REQUIRED\"}]"));
    }

    [Test]
    public void GetCorrelationId_WhenMiddlewareHasRun_ReturnsTheResolvedId()
    {
        var context = CreateContext();

        Assert.That(context.GetCorrelationId(), Is.EqualTo(TraceId));
    }

    [Test]
    public void GetCorrelationId_WhenMiddlewareHasNotRun_ReturnsEmptyRatherThanThrowing()
    {
        var context = new DefaultHttpContext();

        Assert.That(context.GetCorrelationId(), Is.Empty);
    }

    [TestCase("/swagger/index.html", true)]
    [TestCase("/health", true)]
    [TestCase("/hubs/notifications/negotiate", true)]
    [TestCase("/api/projects", false)]
    [TestCase("/healthcheck", false)]
    public void ShouldBypass_MatchesOnPathSegmentBoundaries(string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.That(EnvelopeBypass.ShouldBypass(context, new ApiEnvelopeOptions()), Is.EqualTo(expected));
    }
}
