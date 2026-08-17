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

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class MinimalApiEnvelopeTests
{
    private sealed record Sample(int Id, string Name);

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

    private static async Task<JsonElement> GetEnvelopeAsync(IHost host, string path)
    {
        var response = await host.GetTestClient().GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement;
    }

    [Test]
    public async Task ObjectReturn_IsEnveloped()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/one", () => new Sample(42, "Ahmed")));

        var envelope = await GetEnvelopeAsync(host, "/one");

        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.True);
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(200));
        Assert.That(envelope.GetProperty("result").GetProperty("id").GetInt32(), Is.EqualTo(42));
        Assert.That(envelope.GetProperty("errorCode").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(envelope.GetProperty("correlationId").GetString(), Is.Not.Empty);
    }

    [Test]
    public async Task TypedResultsOk_IsEnveloped()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/two", () => TypedResults.Ok(new Sample(7, "B"))));

        var envelope = await GetEnvelopeAsync(host, "/two");

        Assert.That(envelope.GetProperty("result").GetProperty("id").GetInt32(), Is.EqualTo(7));
    }

    [Test]
    public async Task CreatedResult_KeepsItsStatusCode()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/three", () => TypedResults.Created("/three", new Sample(9, "C"))));

        var response = await host.GetTestClient().GetAsync("/three");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(201));
    }

    [Test]
    public async Task NoContent_BecomesTwoHundredWithANullResult()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/four", () => TypedResults.NoContent()));

        var response = await host.GetTestClient().GetAsync("/four");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(200));
        Assert.That(envelope.GetProperty("result").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task FileResult_IsNotEnveloped()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/file", () => Results.File(new byte[] { 1, 2, 3 }, "application/octet-stream")));

        var response = await host.GetTestClient().GetAsync("/file");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task Redirect_IsNotEnveloped()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/go", () => Results.Redirect("/elsewhere")));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/go");

        Assert.That((int)response.StatusCode, Is.EqualTo(302));
        Assert.That(response.Headers.Location!.ToString(), Is.EqualTo("/elsewhere"));
    }

    [Test]
    public async Task AlreadyEnvelopedReturn_IsNotDoubleWrapped()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/pre", (HttpContext c) =>
                ApiResponse.Success(new Sample(1, "x"), c.GetCorrelationId())));

        var envelope = await GetEnvelopeAsync(host, "/pre");

        Assert.That(envelope.GetProperty("result").GetProperty("id").GetInt32(), Is.EqualTo(1));
        Assert.That(envelope.GetProperty("result").TryGetProperty("isSuccess", out _), Is.False);
    }

    [Test]
    public async Task WithoutApiEnvelope_OptsAnEndpointOut()
    {
        using var host = CreateHost(e =>
        {
            var group = e.MapGroup("").WithApiEnvelope();
            group.MapGet("/raw", () => new Sample(1, "x")).WithoutApiEnvelope();
        });

        var response = await host.GetTestClient().GetAsync("/raw");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(body, Does.Not.Contain("isSuccess"));
        Assert.That(body, Does.Contain("\"id\":1"));
    }

    [Test]
    public async Task TypedResultsBadRequest_IsErrorEnvelopedNotSuccessWrapped()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/badrequest", () => TypedResults.BadRequest(new { field = "code" })));

        var response = await host.GetTestClient().GetAsync("/badrequest");
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(body).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo("BAD_REQUEST"));
        Assert.That(body, Does.Not.Contain("\"field\""));
    }

    [Test]
    public async Task TypedResultsNotFound_IsErrorEnvelopedNotSuccessWrapped()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/notfound", () => TypedResults.NotFound(new { id = 1 })));

        var response = await host.GetTestClient().GetAsync("/notfound");
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(body).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo("NOT_FOUND"));
        Assert.That(body, Does.Not.Contain("\"id\":1"));
    }

    [Test]
    public async Task PagedResult_NestsDataAndPaginationInsideResult()
    {
        using var host = CreateHost(e => e.MapGroup("").WithApiEnvelope()
            .MapGet("/paged", () => Enumerable.Range(1, 5)
                .Select(i => new Sample(i, $"n{i}"))
                .GetPaged(2, 2)));

        var envelope = await GetEnvelopeAsync(host, "/paged");
        var result = envelope.GetProperty("result");

        Assert.That(result.GetProperty("data").GetArrayLength(), Is.EqualTo(2));
        Assert.That(result.GetProperty("pagination").GetProperty("currentPage").GetInt32(), Is.EqualTo(2));
        Assert.That(result.GetProperty("pagination").GetProperty("pageCount").GetInt32(), Is.EqualTo(3));
    }
}
