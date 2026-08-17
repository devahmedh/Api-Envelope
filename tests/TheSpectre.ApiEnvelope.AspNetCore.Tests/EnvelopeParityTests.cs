using System.Net;
using System.Text.RegularExpressions;
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
[Route("p")]
public sealed class ParityTestController : Controller
{
    public sealed record Sample(int Id, string Name);

    [HttpGet("object")]
    public IActionResult Object() => Ok(new Sample(42, "Ahmed"));

    [HttpGet("created")]
    public IActionResult CreatedWithValue() => Created("/p/object", new Sample(9, "C"));

    [HttpGet("nocontent")]
    public IActionResult NoContentAction() => NoContent();

    [HttpGet("void")]
    public void VoidAction()
    {
    }

    [HttpGet("created-empty")]
    public IActionResult CreatedNoValue() => Created("/p/object", (object?)null);

    [HttpGet("accepted-empty")]
    public IActionResult AcceptedNoValue() => Accepted();

    [HttpGet("json")]
    public IActionResult JsonAction() => Json(new Sample(5, "J"));

    [HttpGet("badrequest")]
    public IActionResult BadRequestWithValue() => BadRequest(new { field = "code" });

    [HttpGet("notfound")]
    public IActionResult NotFoundWithValue() => NotFound(new { id = 1 });
}

/// <summary>
/// One table of cases driven through both an MVC host and a minimal-API host, so a divergence
/// between <c>ApiEnvelopeResultFilter</c> and <c>ApiEnvelopeEndpointFilter</c> shows up as a
/// single failing row instead of silently passing two independently written fixtures.
/// </summary>
[TestFixture]
public sealed class EnvelopeParityTests
{
    private sealed record Sample(int Id, string Name);

    private static readonly Regex CorrelationIdPattern =
        new("\"correlationId\":\"[0-9a-f]{32}\"", RegexOptions.Compiled);

    private static string Normalise(string body) =>
        CorrelationIdPattern.Replace(body, "\"correlationId\":\"REDACTED\"");

    private static IHost CreateMvcHost()
    {
        return new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(ParityTestController).Assembly);
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
                    group.MapGet("/p/object", () => new Sample(42, "Ahmed"));
                    group.MapGet("/p/created", () => TypedResults.Created("/p/object", new Sample(9, "C")));
                    group.MapGet("/p/nocontent", () => TypedResults.NoContent());
                    group.MapGet("/p/void", () => { });
                    group.MapGet("/p/created-empty", () => TypedResults.Created("/p/object"));
                    group.MapGet("/p/accepted-empty", () => TypedResults.Accepted("/p/object"));
                    group.MapGet("/p/json", () => TypedResults.Json(new Sample(5, "J")));
                    group.MapGet("/p/badrequest", () => TypedResults.BadRequest(new { field = "code" }));
                    group.MapGet("/p/notfound", () => TypedResults.NotFound(new { id = 1 }));
                });
            });
        }).Start();
    }

    private static IEnumerable<TestCaseData> Cases()
    {
        yield return new TestCaseData("/p/object", HttpStatusCode.OK).SetName("Parity_ObjectSuccess");
        yield return new TestCaseData("/p/created", HttpStatusCode.Created).SetName("Parity_CreatedWithValue");
        yield return new TestCaseData("/p/nocontent", HttpStatusCode.OK).SetName("Parity_NoContent");
        yield return new TestCaseData("/p/void", HttpStatusCode.OK).SetName("Parity_VoidHandler");
        yield return new TestCaseData("/p/created-empty", HttpStatusCode.Created).SetName("Parity_CreatedNoValue");
        yield return new TestCaseData("/p/accepted-empty", HttpStatusCode.Accepted).SetName("Parity_AcceptedNoValue");
        yield return new TestCaseData("/p/json", HttpStatusCode.OK).SetName("Parity_JsonResult");
        yield return new TestCaseData("/p/badrequest", HttpStatusCode.BadRequest).SetName("Parity_BadRequestWithValue");
        yield return new TestCaseData("/p/notfound", HttpStatusCode.NotFound).SetName("Parity_NotFoundWithValue");
    }

    [TestCaseSource(nameof(Cases))]
    public async Task MvcAndMinimalApi_ProduceIdenticalEnvelopesApartFromCorrelationId(
        string path, HttpStatusCode expectedStatus)
    {
        using var mvcHost = CreateMvcHost();
        using var minimalHost = CreateMinimalHost();

        var mvcResponse = await mvcHost.GetTestClient().GetAsync(path);
        var minimalResponse = await minimalHost.GetTestClient().GetAsync(path);

        var mvcBody = Normalise(await mvcResponse.Content.ReadAsStringAsync());
        var minimalBody = Normalise(await minimalResponse.Content.ReadAsStringAsync());

        Assert.That(mvcResponse.StatusCode, Is.EqualTo(expectedStatus));
        Assert.That(minimalResponse.StatusCode, Is.EqualTo(expectedStatus));
        Assert.That(mvcBody, Is.EqualTo(minimalBody));
    }
}
