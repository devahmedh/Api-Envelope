using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[ApiController]
[Route("mvc-metadata")]
public sealed class EnvelopeMetadataTestController : ControllerBase
{
    public sealed record Sample(int Id, string Name);

    [HttpGet("object")]
    [ProducesResponseType(typeof(Sample), StatusCodes.Status200OK)]
    public IActionResult GetObject() => Ok(new Sample(42, "Ahmed"));

    [HttpGet("raw")]
    [NoEnvelope]
    [ProducesResponseType(typeof(Sample), StatusCodes.Status200OK)]
    public IActionResult GetRaw() => Ok(new Sample(1, "x"));
}

[TestFixture]
public sealed class MvcMetadataTests
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
                    .AddApplicationPart(typeof(EnvelopeMetadataTestController).Assembly);
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

    private static ApiDescription GetDescription(IHost host, string relativePath)
    {
        var provider = host.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();
        return provider.ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Single(d => d.RelativePath == relativePath);
    }

    [Test]
    public void ProducesResponseTypeSample_TwoHundredEntry_ReportsApiResponseOfSample()
    {
        using var host = CreateHost();

        var responseTypes = GetDescription(host, "mvc-metadata/object").SupportedResponseTypes;

        var ok = responseTypes.Single(r => r.StatusCode == StatusCodes.Status200OK);
        Assert.That(ok.Type, Is.EqualTo(typeof(ApiResponse<EnvelopeMetadataTestController.Sample>)));
    }

    [Test]
    public void ProducesResponseTypeSample_AddsBadRequestAndServerErrorEntries_TypedApiResponseOfObject()
    {
        using var host = CreateHost();

        var responseTypes = GetDescription(host, "mvc-metadata/object").SupportedResponseTypes;

        var badRequest = responseTypes.Single(r => r.StatusCode == StatusCodes.Status400BadRequest);
        var serverError = responseTypes.Single(r => r.StatusCode == StatusCodes.Status500InternalServerError);

        Assert.That(badRequest.Type, Is.EqualTo(typeof(ApiResponse<object>)));
        Assert.That(serverError.Type, Is.EqualTo(typeof(ApiResponse<object>)));
    }

    [Test]
    public void NoEnvelopeAction_LeavesMetadataUnchanged()
    {
        using var host = CreateHost();

        var responseTypes = GetDescription(host, "mvc-metadata/raw").SupportedResponseTypes;

        var ok = responseTypes.Single(r => r.StatusCode == StatusCodes.Status200OK);
        Assert.That(ok.Type, Is.EqualTo(typeof(EnvelopeMetadataTestController.Sample)));
        Assert.That(responseTypes.Any(r => r.StatusCode == StatusCodes.Status400BadRequest), Is.False);
        Assert.That(responseTypes.Any(r => r.StatusCode == StatusCodes.Status500InternalServerError), Is.False);
    }
}
