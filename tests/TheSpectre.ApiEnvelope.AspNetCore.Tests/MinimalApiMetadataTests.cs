using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class MinimalApiMetadataTests
{
    private sealed record Project(int Id, string Name);

    private static IHost CreateHost(Action<IEndpointRouteBuilder> map)
    {
        var host = new HostBuilder().ConfigureWebHost(web =>
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
                app.UseEndpoints(endpoints => map(endpoints));
            });
        }).Start();

        return host;
    }

    private static RouteEndpoint GetEndpoint(IHost host, string routePattern)
    {
        var dataSource = host.Services.GetRequiredService<EndpointDataSource>();
        return dataSource.Endpoints.OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == routePattern);
    }

    [Test]
    public void WithApiEnvelope_TwoHundredEntry_ReportsApiResponseOfProject()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/project", () => new Project(1, "Ahmed")));

        var responses = GetEndpoint(host, "/project").Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>();

        var ok = responses.Single(r => r.StatusCode == StatusCodes.Status200OK);
        Assert.That(ok.Type, Is.EqualTo(typeof(ApiResponse<Project>)));
    }

    [Test]
    public void WithApiEnvelope_AddsBadRequestAndServerErrorEntries_TypedApiResponseOfObject()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/project", () => new Project(1, "Ahmed")));

        var responses = GetEndpoint(host, "/project").Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>();

        var badRequest = responses.Single(r => r.StatusCode == StatusCodes.Status400BadRequest);
        var serverError = responses.Single(r => r.StatusCode == StatusCodes.Status500InternalServerError);

        Assert.That(badRequest.Type, Is.EqualTo(typeof(ApiResponse<object>)));
        Assert.That(serverError.Type, Is.EqualTo(typeof(ApiResponse<object>)));
    }

    [Test]
    public void WithApiEnvelope_NoContentEndpoint_ReportsTwoHundredNotTwoOhFour()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/none", () => TypedResults.NoContent()));

        var responses = GetEndpoint(host, "/none").Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.That(responses.Any(r => r.StatusCode == StatusCodes.Status204NoContent), Is.False);
        Assert.That(responses.Any(r => r.StatusCode == StatusCodes.Status200OK), Is.True);
    }

    [Test]
    public void WithoutApiEnvelope_LeavesMetadataUnchanged()
    {
        using var host = CreateHost(e =>
        {
            var group = e.MapGroup("").WithApiEnvelope();
            group.MapGet("/raw", () => new Project(1, "Ahmed")).WithoutApiEnvelope();
        });

        var responses = GetEndpoint(host, "/raw").Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>();

        // The rewrite must never run for an opted-out endpoint: no ApiResponse<T> wrapper, and
        // no synthetic 400/500 entries added on its behalf.
        Assert.That(responses.Any(r => r.Type == typeof(ApiResponse<Project>)), Is.False);
        Assert.That(responses.Any(r => r.StatusCode == StatusCodes.Status400BadRequest), Is.False);
        Assert.That(responses.Any(r => r.StatusCode == StatusCodes.Status500InternalServerError), Is.False);
    }
}
