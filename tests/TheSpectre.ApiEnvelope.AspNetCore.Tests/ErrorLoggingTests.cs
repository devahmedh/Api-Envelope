using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

[TestFixture]
public sealed class ErrorLoggingTests
{
    private sealed record CreateProject(string Title);

    /// <summary>
    /// A validation failure is a client input mistake, not a fault. It must not reach the error
    /// stream, and it must carry the failed fields — the entry exists to answer "which field",
    /// which the old VALIDATION_FAILED-only message never did.
    /// </summary>
    [Test]
    public async Task ValidationFailure_LogsUnderTheValidationCategoryAtInformation()
    {
        var (host, recorder) = CreateHost();
        using (host)
        {
            await host.GetTestClient().PostAsJsonAsync("/projects", new { title = "" });
        }

        var entry = recorder.Entries.SingleOrDefault(e => e.Category == LoggerCategories.Validation);

        Assert.Multiple(() =>
        {
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.Level, Is.EqualTo(LogLevel.Information));
            Assert.That(entry.Message, Does.Contain("title"));
            Assert.That(entry.Message, Does.Contain("REQUIRED"));
        });
    }

    /// <summary>
    /// The regression guard on the reclassification. Before this change a validation failure was
    /// an Error entry with a stack trace, so every bad form submission looked like a server fault
    /// on a dashboard. A future change that routes validation back through the generic path would
    /// restore that silently — nothing about the response would differ.
    /// </summary>
    [Test]
    public async Task ValidationFailure_ProducesNoErrorEntryAndAttachesNoException()
    {
        var (host, recorder) = CreateHost();
        using (host)
        {
            await host.GetTestClient().PostAsJsonAsync("/projects", new { title = "" });
        }

        // Scoped to this library's own categories, not every ambient logger in the pipeline:
        // on net8.0, ASP.NET Core's built-in ExceptionHandlerMiddleware unconditionally logs its
        // own Error entry (category Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware,
        // with the exception attached) whenever an exception reaches it, independent of whether a
        // registered IExceptionHandler goes on to claim it — net10.0's build does not. That
        // framework-internal entry is outside EnvelopeResponseWriter's routing and cannot be
        // suppressed from here; what this guard verifies is that nothing THIS library wrote
        // treats a validation failure as a fault.
        var ownEntries = recorder.Entries.Where(e => e.Category
            is LoggerCategories.Validation or LoggerCategories.StatusCode or LoggerCategories.Exception);

        Assert.Multiple(() =>
        {
            Assert.That(ownEntries.Any(e => e.Level >= LogLevel.Error), Is.False);
            Assert.That(ownEntries.All(e => e.Exception is null), Is.True);
        });
    }

    /// <summary>
    /// The discriminator is the error code, not the exception type. A domain AppException is a
    /// server-side outcome and keeps Error, details or no details.
    /// </summary>
    [Test]
    public async Task DomainAppException_StillLogsUnderTheExceptionCategoryAtError()
    {
        var (host, recorder) = CreateHost();
        using (host)
        {
            await host.GetTestClient().GetAsync("/conflict");
        }

        var entry = recorder.Entries.SingleOrDefault(e => e.Category == LoggerCategories.Exception);

        Assert.Multiple(() =>
        {
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.InstanceOf<AppException>());
            Assert.That(entry.Message, Does.Contain("PROJECT_CODE_TAKEN"));
        });
    }

    [Test]
    public async Task BareStatusCode_LogsUnderTheStatusCodeCategoryAtDebug()
    {
        var (host, recorder) = CreateHost();
        using (host)
        {
            await host.GetTestClient().GetAsync("/does-not-exist");
        }

        var entry = recorder.Entries.SingleOrDefault(e => e.Category == LoggerCategories.StatusCode);

        Assert.Multiple(() =>
        {
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.Level, Is.EqualTo(LogLevel.Debug));
            Assert.That(entry.Message, Does.Contain("404"));
        });
    }

    /// <summary>
    /// The only mechanical guard on the claim that this logs metadata, not data. The submitted
    /// value is a sentinel that could only appear in a log entry if the value itself were
    /// written — params carries the constraint bound, never the input.
    /// </summary>
    [Test]
    public async Task NoSubmittedValueAppearsInAnyLogEntry()
    {
        const string Sentinel = "SENTINEL-VALUE-MUST-NOT-BE-LOGGED";

        var (host, recorder) = CreateHost();
        using (host)
        {
            await host.GetTestClient().PostAsJsonAsync("/projects", new { title = Sentinel });
        }

        Assert.That(
            recorder.Entries.Any(e => e.Message.Contains(Sentinel, StringComparison.Ordinal)),
            Is.False,
            "A submitted value reached a log entry.");
    }

#if NET8_0
    /// <summary>
    /// Pins the exact framework category name the README instructs operators to paste into
    /// <c>appsettings.json</c> to silence the framework's duplicate error entry. Nothing else in
    /// this suite verifies that string, so a framework rename would break the README's advice
    /// silently instead of failing a test. Net8.0-only: net10.0 suppresses this framework log for
    /// handled exceptions (see the .NET 10 breaking-change note this library's own docs link to),
    /// so the category never appears there for this scenario.
    /// </summary>
    [Test]
    public async Task UnhandledException_FrameworkLogsUnderTheDocumentedExceptionHandlerMiddlewareCategory()
    {
        var (host, recorder) = CreateHost();
        using (host)
        {
            await host.GetTestClient().GetAsync("/conflict");
        }

        var entry = recorder.Entries.SingleOrDefault(e =>
            e.Category == "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware");

        Assert.That(entry, Is.Not.Null);
    }
#endif

    /// <summary>
    /// Logging is a diagnostic aid, not part of the response contract. A host-registered
    /// <see cref="ILoggerProvider"/> that throws (a remote sink failing on a transient network
    /// error, for instance) must not be able to take the error-response path down with it — the
    /// caller must still receive the complete, correct envelope.
    /// </summary>
    [Test]
    public async Task LoggerProviderThatThrows_StillDeliversTheCompleteErrorEnvelope()
    {
        using var host = CreateHostWithThrowingLogger();

        var response = await host.GetTestClient().GetAsync("/conflict");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(root.GetProperty("isSuccess").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("statusCode").GetInt32(), Is.EqualTo(409));
            Assert.That(root.GetProperty("errorCode").GetString(), Is.EqualTo("PROJECT_CODE_TAKEN"));
        });
    }

    private static IHost CreateHostWithThrowingLogger() =>
        new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddApiEnvelope();
                services.AddRouting();
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(new ThrowingLoggerProvider());
                    logging.SetMinimumLevel(LogLevel.Trace);
                });
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/conflict",
                            void () => throw new AppException(
                                "PROJECT_CODE_TAKEN", statusCode: StatusCodes.Status409Conflict))
                        .WithApiEnvelope();
                });
            });
        }).Start();

    private static (IHost Host, RecordingLoggerProvider Recorder) CreateHost()
    {
        var recorder = new RecordingLoggerProvider();

        var host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddApiEnvelope();
                services.AddRouting();
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(recorder);
                    logging.SetMinimumLevel(LogLevel.Trace);
                });
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    // Throws the same envelope DataAnnotationsActionFilter throws, without
                    // pulling MVC into the host. The filter's own mapping is covered by the
                    // DataAnnotations test project; what matters here is only how a
                    // VALIDATION_FAILED AppException is logged.
                    //
                    // The submitted value is deliberately placed in params. Without it the
                    // sentinel test below would assert nothing — the value would have had no
                    // route into a log entry in the first place, and the test would pass
                    // against any implementation, including one that logged params in full.
                    endpoints.MapPost("/projects", void (CreateProject body) =>
                            throw new AppException(
                                ErrorCodes.ValidationFailed,
                                StatusCodes.Status400BadRequest,
                                new[]
                                {
                                    new ErrorDetail(
                                        "title",
                                        ValidationErrorCodes.Required,
                                        new Dictionary<string, object?> { ["attempted"] = body.Title }),
                                }))
                        .WithApiEnvelope();

                    endpoints.MapGet("/conflict",
                            void () => throw new AppException(
                                "PROJECT_CODE_TAKEN", statusCode: StatusCodes.Status409Conflict))
                        .WithApiEnvelope();
                });
            });
        }).Start();

        return (host, recorder);
    }
}
