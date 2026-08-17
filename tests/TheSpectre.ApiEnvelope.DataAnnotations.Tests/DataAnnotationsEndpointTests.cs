using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System.ComponentModel.DataAnnotations;
using TheSpectre.ApiEnvelope.AspNetCore;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Tests;

public sealed class CreateProject
{
    [Required(ErrorMessage = ValidationErrorCodes.Required)]
    public string? Code { get; set; }

    [StringLength(5, ErrorMessage = ValidationErrorCodes.TooLong)]
    public string? Title { get; set; }
}

[ApiController]
[Route("projects")]
public sealed class ProjectsController : ControllerBase
{
    [HttpPost]
    public IActionResult Create(CreateProject model) => Ok(model);
}

[TestFixture]
public sealed class DataAnnotationsEndpointTests
{
    private static IHost CreateHost() =>
        new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(ProjectsController).Assembly);
                services.AddApiEnvelope();
                services.AddApiEnvelopeDataAnnotations();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        }).Start();

    [Test]
    public async Task InvalidModel_ProducesValidationFailedWithPerFieldKeys()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient()
            .PostAsJsonAsync("/projects", new CreateProject { Title = "far too long" });
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo(ErrorCodes.ValidationFailed));

        var fields = envelope.GetProperty("details").EnumerateArray()
            .Select(d => d.GetProperty("field").GetString()).ToArray();

        Assert.That(fields, Is.EquivalentTo(new[] { "code", "title" }));
    }

    [Test]
    public async Task InvalidModel_NeverPutsEnglishOnTheWire()
    {
        using var host = CreateHost();

        var body = await (await host.GetTestClient()
            .PostAsJsonAsync("/projects", new CreateProject { Title = "far too long" }))
            .Content.ReadAsStringAsync();

        Assert.That(body, Does.Not.Contain("is required"));
        Assert.That(body, Does.Not.Contain("One or more validation errors"));
        Assert.That(body, Does.Not.Contain("maximum length"));
    }

    [Test]
    public async Task InvalidModel_CarriesTheBoundAsAParam()
    {
        using var host = CreateHost();

        var envelope = JsonDocument.Parse(await (await host.GetTestClient()
            .PostAsJsonAsync("/projects", new CreateProject { Code = "C1", Title = "far too long" }))
            .Content.ReadAsStringAsync()).RootElement;

        var titleDetail = envelope.GetProperty("details").EnumerateArray()
            .Single(d => d.GetProperty("field").GetString() == "title");

        Assert.That(titleDetail.GetProperty("errorCode").GetString(), Is.EqualTo(ValidationErrorCodes.TooLong));
        Assert.That(titleDetail.GetProperty("params").GetProperty("max").GetInt32(), Is.EqualTo(5));
    }

    [Test]
    public async Task ValidModel_PassesThroughAndIsEnveloped()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient()
            .PostAsJsonAsync("/projects", new CreateProject { Code = "C1", Title = "ok" });
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.True);
    }
}
