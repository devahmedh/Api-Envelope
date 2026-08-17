using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[ApiController]
[Route("mvc")]
public sealed class EnvelopeTestController : ControllerBase
{
    public sealed record Sample(int Id, string Name);

    public sealed class ValidatedModel
    {
        [Required]
        public string? Name { get; set; }
    }

    [HttpGet("object")]
    public IActionResult GetObject() => Ok(new Sample(42, "Ahmed"));

    [HttpGet("created")]
    public IActionResult GetCreated() => Created("/mvc/object", new Sample(9, "C"));

    [HttpGet("nocontent")]
    public IActionResult GetNoContent() => NoContent();

    [HttpGet("file")]
    public IActionResult GetFile() => File(new byte[] { 1, 2, 3 }, "application/octet-stream");

    [HttpGet("raw")]
    [NoEnvelope]
    public IActionResult GetRaw() => Ok(new Sample(1, "x"));

    [HttpGet("badrequest")]
    public IActionResult GetBadRequest() => BadRequest(new { field = "code" });

    [HttpGet("notfound")]
    public IActionResult GetNotFound() => NotFound(new { id = 1 });

    [HttpPost("validate")]
    public IActionResult PostValidate([FromBody] ValidatedModel model) => Ok(model);
}

[TestFixture]
public sealed class MvcEnvelopeTests
{
    private static IHost CreateHost()
    {
        return new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(EnvelopeTestController).Assembly);
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

    private static async Task<JsonElement> GetEnvelopeAsync(IHost host, string path)
    {
        var response = await host.GetTestClient().GetAsync(path);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Test]
    public async Task OkObjectResult_IsEnveloped()
    {
        using var host = CreateHost();

        var envelope = await GetEnvelopeAsync(host, "/mvc/object");

        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.True);
        Assert.That(envelope.GetProperty("result").GetProperty("id").GetInt32(), Is.EqualTo(42));
        Assert.That(envelope.GetProperty("correlationId").GetString(), Is.Not.Empty);
    }

    [Test]
    public async Task CreatedResult_KeepsItsStatusCode()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient().GetAsync("/mvc/created");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(201));
    }

    [Test]
    public async Task NoContent_BecomesTwoHundredWithANullResult()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient().GetAsync("/mvc/nocontent");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(envelope.GetProperty("result").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task FileResult_IsNotEnveloped()
    {
        using var host = CreateHost();

        var bytes = await host.GetTestClient().GetByteArrayAsync("/mvc/file");

        Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task NoEnvelopeAction_IsNotEnveloped()
    {
        using var host = CreateHost();

        var body = await host.GetTestClient().GetStringAsync("/mvc/raw");

        Assert.That(body, Does.Not.Contain("isSuccess"));
        Assert.That(body, Does.Contain("\"id\":1"));
    }

    [Test]
    public async Task BadRequestObjectResult_IsErrorEnvelopedNotSuccessWrapped()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient().GetAsync("/mvc/badrequest");
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(body).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo("BAD_REQUEST"));
        Assert.That(body, Does.Not.Contain("\"field\""));
    }

    [Test]
    public async Task NotFoundObjectResult_IsErrorEnvelopedNotSuccessWrapped()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient().GetAsync("/mvc/notfound");
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(body).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo("NOT_FOUND"));
        Assert.That(body, Does.Not.Contain("\"id\":1"));
    }

    [Test]
    public async Task InvalidModelState_IsErrorEnvelopedWithoutBackendAuthoredProse()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient().PostAsync(
            "/mvc/validate", new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(body).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(body, Does.Not.Contain("One or more validation errors"));
        Assert.That(body, Does.Not.Contain("\"title\""));
    }
}
