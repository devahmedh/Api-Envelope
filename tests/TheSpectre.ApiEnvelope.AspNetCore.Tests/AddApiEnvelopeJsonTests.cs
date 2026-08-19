using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

/// <summary>
/// One registration call has to reach both of ASP.NET Core's serializer option objects.
/// Asserting only one pipeline would pass for a helper that writes to only one, which is
/// exactly the bug this method exists to prevent.
/// </summary>
[TestFixture]
public sealed class AddApiEnvelopeJsonTests
{
    private const string Converted = "2026-08-18T09:30:00Z";
    private const string Unconverted = "2026-08-18T09:30:00";

    [Test]
    public async Task AddApiEnvelopeJson_AppliesToControllersAndMinimalApisFromOneCall()
    {
        using var host = CreateHost(json =>
            json.Converters.Add(new UtcDateTimeJsonConverter()));

        var client = host.GetTestClient();

        Assert.Multiple(async () =>
        {
            Assert.That(await ReadTimestampAsync(client, "/json-options/reading"), Is.EqualTo(Converted));
            Assert.That(await ReadTimestampAsync(client, "/minimal/reading"), Is.EqualTo(Converted));
        });
    }

    [Test]
    public async Task WithoutTheHelper_NeitherPipelineConverts()
    {
        using var host = CreateHost(static _ => { });

        var client = host.GetTestClient();

        Assert.Multiple(async () =>
        {
            Assert.That(await ReadTimestampAsync(client, "/json-options/reading"), Is.EqualTo(Unconverted));
            Assert.That(await ReadTimestampAsync(client, "/minimal/reading"), Is.EqualTo(Unconverted));
        });
    }

    /// <summary>
    /// Registering MVC's options in an application that never adds MVC must be inert, not a
    /// startup failure — that is what keeps the method free of a branch on "is MVC present".
    /// </summary>
    [Test]
    public async Task AddApiEnvelopeJson_WorksInAMinimalApiOnlyHost()
    {
        using var host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddApiEnvelopeJson(json =>
                    json.Converters.Add(new UtcDateTimeJsonConverter()));
                services.AddApiEnvelope();
                services.AddRouting();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints
                    .MapGet("/minimal/reading",
                        () => new JsonOptionsTestController.Reading(JsonOptionsTestController.Sample))
                    .WithApiEnvelope());
            });
        }).Start();

        var timestamp = await ReadTimestampAsync(host.GetTestClient(), "/minimal/reading");

        Assert.That(timestamp, Is.EqualTo(Converted));
    }

    [Test]
    public void AddApiEnvelopeJson_ThrowsOnNullArguments()
    {
        var services = new ServiceCollection();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(
                () => ApiEnvelopeServiceCollectionExtensions.AddApiEnvelopeJson(null!, static _ => { }));
            Assert.Throws<ArgumentNullException>(
                () => services.AddApiEnvelopeJson(null!));
        });
    }

    private static IHost CreateHost(Action<JsonSerializerOptions> configure)
    {
        return new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddApiEnvelopeJson(configure);
                services.AddControllers()
                    .AddApplicationPart(typeof(JsonOptionsTestController).Assembly);
                services.AddApiEnvelope();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapGet("/minimal/reading",
                            () => new JsonOptionsTestController.Reading(JsonOptionsTestController.Sample))
                        .WithApiEnvelope();
                });
            });
        }).Start();
    }

    private static async Task<string?> ReadTimestampAsync(HttpClient client, string path)
    {
        var body = await client.GetStringAsync(path);

        return JsonDocument.Parse(body).RootElement
            .GetProperty("result")
            .GetProperty("timestamp")
            .GetString();
    }
}
