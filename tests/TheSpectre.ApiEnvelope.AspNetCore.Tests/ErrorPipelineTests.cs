using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class ErrorPipelineTests
{
    private static IHost CreateHost(
        Action<IEndpointRouteBuilder> map,
        // "Production" as a literal, not Environments.Production: that field is static
        // readonly rather than const, so it cannot be a default parameter value (CS1736).
        string environment = "Production",
        Action<ApiEnvelopeOptions>? configure = null)
    {
        var builder = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment(environment);
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                if (configure is null)
                {
                    services.AddApiEnvelope();
                }
                else
                {
                    services.AddApiEnvelope(configure);
                }
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints => map(endpoints));
            });
        });

        var host = builder.Start();
        return host;
    }

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement;
    }

    [Test]
    public async Task UnhandledException_InProduction_ReturnsGenericFiveHundredWithoutLeakingTheMessage()
    {
        using var host = CreateHost(e => e.MapGet("/boom",
            void () => throw new InvalidOperationException("Server=tcp:prod-sql;Password=hunter2")));

        var response = await host.GetTestClient().GetAsync("/boom");
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(body).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo(ErrorCodes.InternalError));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(body, Does.Not.Contain("hunter2"));
        Assert.That(body, Does.Not.Contain("InvalidOperationException"));
        Assert.That(envelope.TryGetProperty("message", out _), Is.False);
    }

    [Test]
    public async Task UnhandledException_InDevelopment_IncludesTheDiagnosticMessage()
    {
        using var host = CreateHost(
            e => e.MapGet("/boom", void () => throw new InvalidOperationException("diagnostic detail")),
            Environments.Development);

        var response = await host.GetTestClient().GetAsync("/boom");
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(body).RootElement;

        Assert.That(envelope.GetProperty("message").GetString(), Does.Contain("diagnostic detail"));
        // A regression to exception.ToString() would still satisfy the assertion above (it
        // contains the message too), so pin the negative case as well: only the message, never
        // the type name or a stack frame, may reach the wire.
        Assert.That(body, Does.Not.Contain("InvalidOperationException"));
        Assert.That(body, Does.Not.Contain("   at "));
    }

    [Test]
    public async Task AppException_UsesItsOwnStatusAndErrorKey()
    {
        using var host = CreateHost(e => e.MapGet("/conflict",
            void () => throw new AppException("PROJECT_CODE_TAKEN", statusCode: 409)));

        var response = await host.GetTestClient().GetAsync("/conflict");
        var envelope = await ReadEnvelopeAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo("PROJECT_CODE_TAKEN"));
    }

    [Test]
    public async Task RoutingNotFound_IsEnvelopedEvenThoughNoEndpointRan()
    {
        using var host = CreateHost(e => e.MapGet("/known", () => "ok"));

        var response = await host.GetTestClient().GetAsync("/does-not-exist");
        var envelope = await ReadEnvelopeAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo(ErrorCodes.NotFound));
        Assert.That(envelope.GetProperty("statusCode").GetInt32(), Is.EqualTo(404));
    }

    [Test]
    public async Task CorrelationId_IsEchoedOnASuccessResponseToo()
    {
        using var host = CreateHost(e => e.MapGet("/ok", () => "fine"));

        var response = await host.GetTestClient().GetAsync("/ok");

        Assert.That(response.Headers.Contains("X-Correlation-Id"), Is.True);
        Assert.That(response.Headers.GetValues("X-Correlation-Id").Single(), Is.Not.Empty);
    }

    [Test]
    public async Task CorrelationId_IsEchoedOnAnErrorResponseToo()
    {
        // ExceptionHandlerMiddlewareImpl calls HttpResponse.Clear() before any IExceptionHandler
        // runs, which wipes the header CorrelationIdMiddleware set before the request ever
        // reached the endpoint. Only EnvelopeResponseWriter re-setting it survives that clear.
        using var host = CreateHost(e => e.MapGet("/boom",
            void () => throw new InvalidOperationException("x")));

        var response = await host.GetTestClient().GetAsync("/boom");

        Assert.That(response.Headers.Contains("X-Correlation-Id"), Is.True);
        Assert.That(response.Headers.GetValues("X-Correlation-Id").Single(), Is.Not.Empty);
    }

    [Test]
    public async Task CorrelationId_WithWellFormedInboundHeader_IsEchoedBack()
    {
        using var host = CreateHost(e => e.MapGet("/ok", () => "fine"));
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "inbound-123");

        var response = await client.GetAsync("/ok");

        Assert.That(response.Headers.GetValues("X-Correlation-Id").Single(), Is.EqualTo("inbound-123"));
    }

    [Test]
    public async Task CorrelationId_WithMalformedInboundHeader_IsReplacedNotEchoed()
    {
        using var host = CreateHost(e => e.MapGet("/ok", () => "fine"));
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-Id", "bad value");

        var response = await client.GetAsync("/ok");

        Assert.That(response.Headers.GetValues("X-Correlation-Id").Single(), Is.Not.EqualTo("bad value"));
    }

    [Test]
    public async Task ExcludedPath_IsNotEnveloped()
    {
        using var host = CreateHost(e => e.MapGet("/health", () => Results.StatusCode(503)));

        var response = await host.GetTestClient().GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(body, Does.Not.Contain("isSuccess"));
    }

    [Test]
    public async Task NoEnvelopeEndpoint_IsNotEnveloped()
    {
        using var host = CreateHost(e => e
            .MapGet("/raw", () => Results.StatusCode(404))
            .WithMetadata(new NoEnvelopeAttribute()));

        var response = await host.GetTestClient().GetAsync("/raw");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(body, Does.Not.Contain("isSuccess"));
    }

    [Test]
    public async Task NoEnvelopeEndpoint_IsNotEnvelopedEvenWhenItThrows()
    {
        // The endpoint that throws carries [NoEnvelope], but by the time
        // ApiEnvelopeExceptionHandler runs, ExceptionHandlerMiddlewareImpl.ClearHttpContext()
        // has already called SetEndpoint(null) — so EnvelopeBypass can only see it via the copy
        // StatusCodeEnvelopeMiddleware stashes in HttpContext.Items during the unwind.
        using var host = CreateHost(e => e
            .MapGet("/raw-boom", void () => throw new InvalidOperationException("x"))
            .WithMetadata(new NoEnvelopeAttribute()));

        var response = await host.GetTestClient().GetAsync("/raw-boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(body, Does.Not.Contain("isSuccess"));
    }

    [Test]
    public async Task BareUnauthorized_IsEnvelopedAsUnauthorized()
    {
        // The case StatusCodeEnvelopeMiddleware exists for: UseAuthorization's 401 challenge
        // short-circuits before any endpoint (or filter) runs, so nothing but this middleware
        // ever sees it.
        var builder = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddApiEnvelope();
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, NoOpAuthenticationHandler>("Test", null);
                services.AddAuthorization();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints
                    .MapGet("/secure", () => "secret")
                    .RequireAuthorization());
            });
        });

        using var host = builder.Start();

        var response = await host.GetTestClient().GetAsync("/secure");
        var envelope = await ReadEnvelopeAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo(ErrorCodes.Unauthorized));
    }

    [Test]
    public async Task MessageVisibilityNever_SuppressesTheMessageEvenInDevelopment()
    {
        using var host = CreateHost(
            e => e.MapGet("/boom", void () => throw new InvalidOperationException("detail")),
            Environments.Development,
            o => o.MessageVisibility = MessageVisibility.Never);

        var response = await host.GetTestClient().GetAsync("/boom");
        var envelope = await ReadEnvelopeAsync(response);

        Assert.That(envelope.TryGetProperty("message", out _), Is.False);
    }

    // Always declines to authenticate, so RequireAuthorization()'s default challenge runs —
    // which, with no cookie or OAuth redirect configured, is a bare 401 with an empty body.
    // That bare response is exactly what StatusCodeEnvelopeMiddleware exists to envelope.
    private sealed class NoOpAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());
    }
}
