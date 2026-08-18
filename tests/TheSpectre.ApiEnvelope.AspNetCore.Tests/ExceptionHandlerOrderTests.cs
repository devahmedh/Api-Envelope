using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

/// <summary>Writes an RFC 7807 body and claims the exception, the way most existing apps do.</summary>
internal sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            """{"type":"about:blank","title":"An error occurred","status":500}""",
            cancellationToken);

        return true;
    }
}

/// <summary>Logs and declines, which is what a handler must do to coexist with the envelope.</summary>
internal sealed class LogOnlyExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken) => ValueTask.FromResult(false);
}

/// <summary>
/// Adoption report F-2: an <see cref="IExceptionHandler"/> registered before
/// <c>AddApiEnvelope()</c> runs first and swallows every enveloped error.
/// </summary>
[TestFixture]
public sealed class ExceptionHandlerOrderTests
{
    [Test]
    public void Describe_NamesHandlersRegisteredBeforeTheEnvelope()
    {
        var services = new ServiceCollection();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        Assert.That(
            ExceptionHandlerOrder.Describe(services),
            Is.EqualTo(new[] { nameof(ProblemDetailsExceptionHandler) }));
    }

    [Test]
    public void Describe_IgnoresTheEnvelopesOwnHandler()
    {
        var services = new ServiceCollection();
        services.AddApiEnvelope();

        // A second call must not report the first call's handler as preceding it.
        Assert.That(ExceptionHandlerOrder.Describe(services), Is.Empty);
    }

    /// <summary>
    /// The capture has to happen at the moment <c>AddApiEnvelope()</c> runs, not later:
    /// a handler registered afterwards runs after this library's and is not a problem, so it
    /// must not appear in what was captured.
    /// </summary>
    [Test]
    public void Capture_RecordsOnlyWhatWasRegisteredBeforeAddApiEnvelope()
    {
        var services = new ServiceCollection();
        services.AddApiEnvelope();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        var captured = services.BuildServiceProvider().GetRequiredService<ExceptionHandlerOrder>();

        Assert.That(captured.PrecedingHandlers, Is.Empty);
    }

    [Test]
    public void Capture_RecordsAHandlerRegisteredBeforeAddApiEnvelope()
    {
        var services = new ServiceCollection();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        services.AddApiEnvelope();

        var captured = services.BuildServiceProvider().GetRequiredService<ExceptionHandlerOrder>();

        Assert.That(
            captured.PrecedingHandlers,
            Is.EqualTo(new[] { nameof(ProblemDetailsExceptionHandler) }));
    }

    [Test]
    public void UseApiEnvelope_WarnsWhenAnotherHandlerIsRegisteredFirst()
    {
        var sink = new WarningSink();

        using (StartHost(sink, services =>
        {
            services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
            services.AddApiEnvelope();
        }))
        {
            Assert.That(
                sink.Warnings,
                Has.One.Contains(nameof(ProblemDetailsExceptionHandler))
                    .And.One.Contains("runs first"));
        }
    }

    [Test]
    public void UseApiEnvelope_IsSilentWhenTheEnvelopeIsRegisteredFirst()
    {
        var sink = new WarningSink();

        using (StartHost(sink, services =>
        {
            services.AddApiEnvelope();
            services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        }))
        {
            Assert.That(sink.Warnings, Is.Empty);
        }
    }

    /// <summary>
    /// The behaviour the warning exists to describe: a preceding handler claims the exception,
    /// so <see cref="AppException"/>'s 409 arrives at the client as a 500 with a ProblemDetails
    /// body. Asserting this pins what the warning is actually warning about.
    /// </summary>
    [Test]
    public async Task PrecedingHandler_SwallowsTheEnvelopeAndItsStatusCode()
    {
        using var host = StartThrowingHost(services =>
        {
            services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
            services.AddApiEnvelope();
        });

        var response = await host.GetTestClient().GetAsync("/boom");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("about:blank"));
    }

    /// <summary>The documented remedy: the other handler logs and returns false.</summary>
    [Test]
    public async Task DecliningHandler_LetsTheEnvelopeWriteItsOwnResponse()
    {
        using var host = StartThrowingHost(services =>
        {
            services.AddExceptionHandler<LogOnlyExceptionHandler>();
            services.AddApiEnvelope();
        });

        var response = await host.GetTestClient().GetAsync("/boom");
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo("PROJECT_CODE_TAKEN"));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
    }

    private static IHost StartHost(WarningSink sink, Action<IServiceCollection> configureServices)
    {
        return new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddLogging(logging => logging
                    .SetMinimumLevel(LogLevel.Warning)
                    .AddProvider(sink));
                configureServices(services);
            });
            web.Configure(app => app.UseApiEnvelope());
        }).Start();
    }

    private static IHost StartThrowingHost(Action<IServiceCollection> configureServices)
    {
        return new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                configureServices(services);
                services.AddRouting();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints
                    .MapGet("/boom", void () => throw new AppException(
                        "PROJECT_CODE_TAKEN", statusCode: StatusCodes.Status409Conflict))
                    .WithApiEnvelope());
            });
        }).Start();
    }
}
