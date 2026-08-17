using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using TheSpectre.ApiEnvelope;
using TheSpectre.ApiEnvelope.AspNetCore;

namespace ConsumerSmoke;

[TestFixture]
public sealed class ConsumerSmokeTests
{
    private sealed record Project(int Id, string Name);

    private static IHost CreateHost() =>
        new HostBuilder().ConfigureWebHost(web =>
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
                    var api = endpoints.MapGroup("").WithApiEnvelope();
                    api.MapGet("/project", () => new Project(42, "Ahmed"));
                    api.MapGet("/paged", () => Enumerable.Range(1, 5)
                        .Select(i => new Project(i, $"n{i}"))
                        .GetPaged(2, 2));
                    api.MapGet("/conflict", void () =>
                        throw new AppException("PROJECT_CODE_TAKEN", statusCode: 409));
                });
            });
        }).Start();

    private static async Task<JsonElement> GetAsync(IHost host, string path)
    {
        var response = await host.GetTestClient().GetAsync(path);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Test]
    public async Task InstalledFromAPackage_SuccessResponseIsEnveloped()
    {
        using var host = CreateHost();

        var envelope = await GetAsync(host, "/project");

        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.True);
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(200));
        Assert.That(envelope.GetProperty("result").GetProperty("id").GetInt32(), Is.EqualTo(42));
        Assert.That(envelope.GetProperty("errorCode").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(envelope.GetProperty("correlationId").GetString(), Is.Not.Empty);
    }

    [Test]
    public async Task InstalledFromAPackage_PagedResultNestsDataAndPagination()
    {
        using var host = CreateHost();

        var result = (await GetAsync(host, "/paged")).GetProperty("result");

        Assert.That(result.GetProperty("data").GetArrayLength(), Is.EqualTo(2));
        Assert.That(result.GetProperty("pagination").GetProperty("currentPage").GetInt32(), Is.EqualTo(2));
        Assert.That(result.GetProperty("pagination").GetProperty("pageCount").GetInt32(), Is.EqualTo(3));
    }

    [Test]
    public async Task InstalledFromAPackage_AppExceptionProducesItsErrorKey()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient().GetAsync("/conflict");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo("PROJECT_CODE_TAKEN"));
    }

    [Test]
    public async Task InstalledFromAPackage_CorrelationIdIsEchoedOnEveryResponse()
    {
        using var host = CreateHost();

        var success = await host.GetTestClient().GetAsync("/project");
        var failure = await host.GetTestClient().GetAsync("/conflict");

        Assert.That(success.Headers.Contains("X-Correlation-Id"), Is.True);
        Assert.That(failure.Headers.Contains("X-Correlation-Id"), Is.True);
    }
}
