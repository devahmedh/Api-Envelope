using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[ApiController]
[Route("bodiless")]
public sealed class BodilessTestController : ControllerBase
{
    [HttpGet("ok")]
    public IActionResult GetOk() => Ok();

    [HttpGet("statuscode202")]
    public IActionResult GetStatusCode202() => StatusCode(202);

    [HttpGet("content")]
    public IActionResult GetContent() => Content("hello");

    [HttpGet("redirect")]
    public IActionResult GetRedirect() => Redirect("/x");

    [HttpGet("file")]
    public IActionResult GetFile() => File(new byte[] { 1, 2, 3 }, "application/octet-stream");
}

/// <summary>
/// Covers the bodiless-2xx hole: <c>Ok()</c> and <c>StatusCode(202)</c> ship with an empty body
/// today on both stacks, forcing a client to check the status before daring to read the body -
/// the exact branching this library exists to remove. The pass-through cases in this fixture
/// (<c>Content</c>, redirects, files) are the regression guard proving the fix does not start
/// enveloping things that must stay raw.
/// </summary>
[TestFixture]
public sealed class BodilessSuccessTests
{
    private sealed record Sample(int Id, string Name);

    private static IHost CreateMvcHost()
    {
        return new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(BodilessTestController).Assembly);
                services.AddApiEnvelope();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        }).Start();
    }

    private static IHost CreateMinimalHost()
    {
        return new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddApiEnvelope();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    var group = endpoints.MapGroup("").WithApiEnvelope();
                    group.MapGet("/ok", () => TypedResults.Ok());
                    group.MapGet("/statuscode202", () => Results.StatusCode(202));
                    group.MapGet("/text", () => Results.Text("hello"));
                    group.MapGet("/redirect", () => Results.Redirect("/x"));
                });
            });
        }).Start();
    }

    // --- Bodiless 2xx cases: must now be enveloped -------------------------------------------

    [Test]
    public async Task Mvc_OkWithNoValue_IsEnvelopedWithNullResult()
    {
        using var host = CreateMvcHost();

        var response = await host.GetTestClient().GetAsync("/bodiless/ok");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.True);
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(200));
        Assert.That(envelope.GetProperty("result").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Mvc_StatusCode202_IsEnvelopedWithNullResult()
    {
        using var host = CreateMvcHost();

        var response = await host.GetTestClient().GetAsync("/bodiless/statuscode202");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That((int)response.StatusCode, Is.EqualTo(202));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.True);
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(202));
        Assert.That(envelope.GetProperty("result").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Minimal_TypedResultsOkNonGeneric_IsEnvelopedWithNullResult()
    {
        using var host = CreateMinimalHost();

        var response = await host.GetTestClient().GetAsync("/ok");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.True);
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(200));
        Assert.That(envelope.GetProperty("result").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Minimal_ResultsStatusCode202_IsEnvelopedWithNullResult()
    {
        using var host = CreateMinimalHost();

        var response = await host.GetTestClient().GetAsync("/statuscode202");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That((int)response.StatusCode, Is.EqualTo(202));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.True);
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(202));
        Assert.That(envelope.GetProperty("result").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    // --- Pass-through cases: must stay exactly as they were ----------------------------------

    [Test]
    public async Task Mvc_Content_StaysRawTextUnenveloped()
    {
        using var host = CreateMvcHost();

        var response = await host.GetTestClient().GetAsync("/bodiless/content");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body, Is.EqualTo("hello"));
    }

    [Test]
    public async Task Minimal_ResultsText_StaysRawTextUnenveloped()
    {
        using var host = CreateMinimalHost();

        var response = await host.GetTestClient().GetAsync("/text");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body, Is.EqualTo("hello"));
    }

    [Test]
    public async Task Mvc_Redirect_StaysUnenvelopedWithLocationHeader()
    {
        using var host = CreateMvcHost();

        var response = await host.GetTestClient().GetAsync("/bodiless/redirect");

        Assert.That((int)response.StatusCode, Is.EqualTo(302));
        Assert.That(response.Headers.Location!.ToString(), Is.EqualTo("/x"));
    }

    [Test]
    public async Task Minimal_ResultsRedirect_StaysUnenvelopedWithLocationHeader()
    {
        using var host = CreateMinimalHost();

        var response = await host.GetTestClient().GetAsync("/redirect");

        Assert.That((int)response.StatusCode, Is.EqualTo(302));
        Assert.That(response.Headers.Location!.ToString(), Is.EqualTo("/x"));
    }

    [Test]
    public async Task Mvc_File_StaysExactBytesUnenveloped()
    {
        using var host = CreateMvcHost();

        var bytes = await host.GetTestClient().GetByteArrayAsync("/bodiless/file");

        Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
    }
}
