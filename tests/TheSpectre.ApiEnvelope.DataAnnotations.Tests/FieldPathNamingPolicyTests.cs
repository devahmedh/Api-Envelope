using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Tests;

public sealed class NamingModel
{
    [Required(ErrorMessage = ValidationErrorCodes.Required)]
    public string? Code { get; set; }

    /// <summary>Two words, so the naming policy in force is visible in the emitted path.</summary>
    [Required(ErrorMessage = ValidationErrorCodes.Required)]
    public string? OwnerName { get; set; }
}

[ApiController]
[Route("naming")]
public sealed class NamingController : ControllerBase
{
    [HttpPost]
    public IActionResult Create(NamingModel model) => Ok(model);
}

/// <summary>
/// Which option object supplies the naming policy that turns a CLR property path into the
/// <c>details[].field</c> path the client matches against its form controls.
/// </summary>
/// <remarks>
/// Adoption report F-1, applied to the validation path. MVC binds the request body with
/// <c>Mvc.JsonOptions</c>, so the field name the client sent was shaped by that policy. Reading
/// the naming policy from <c>Http.Json.JsonOptions</c> instead would emit a path under a policy
/// the request never used, and a client attaching errors to controls by field name would attach
/// none of them — with a 400 that otherwise looks completely correct.
/// </remarks>
[TestFixture]
public sealed class FieldPathNamingPolicyTests
{
    [Test]
    public async Task FieldPath_FollowsTheNamingPolicyConfiguredOnMvc()
    {
        using var host = CreateHost(json =>
            json.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        var fields = await PostInvalidAsync(host);

        Assert.That(fields, Does.Contain("code"));
    }

    /// <summary>
    /// A two-word property makes the policy visible: camelCase gives <c>ownerName</c>,
    /// snake_case gives <c>owner_name</c>. Anything else means the wrong options object was read.
    /// </summary>
    [Test]
    public async Task FieldPath_UsesSnakeCase_WhenMvcIsConfiguredForIt()
    {
        using var host = CreateHost(json =>
            json.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        var fields = await PostInvalidAsync(host);

        Assert.That(fields, Does.Contain("owner_name"));
    }

    [Test]
    public async Task FieldPath_UsesCamelCase_ByDefault()
    {
        using var host = CreateHost(static _ => { });

        var fields = await PostInvalidAsync(host);

        Assert.That(fields, Does.Contain("ownerName"));
    }

    private static async Task<string?[]> PostInvalidAsync(IHost host)
    {
        var response = await host.GetTestClient()
            .PostAsJsonAsync("/naming", new Dictionary<string, string?>());

        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        return envelope.GetProperty("details").EnumerateArray()
            .Select(detail => detail.GetProperty("field").GetString())
            .ToArray();
    }

    private static IHost CreateHost(Action<Microsoft.AspNetCore.Mvc.JsonOptions> configure) =>
        new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(NamingController).Assembly)
                    .AddJsonOptions(configure);
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
}
