using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

/// <summary>Captures every warning a host writes, so a startup diagnostic can be asserted on.</summary>
internal sealed class WarningSink : ILoggerProvider
{
    private readonly List<string> _warnings = [];

    internal IReadOnlyList<string> Warnings
    {
        get
        {
            lock (_warnings)
            {
                return _warnings.ToArray();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new SinkLogger(this);

    public void Dispose()
    {
        // Nothing to release: the sink outlives the provider so a test can read it after the
        // host is disposed.
    }

    private void Add(string message)
    {
        lock (_warnings)
        {
            _warnings.Add(message);
        }
    }

    private sealed class SinkLogger(WarningSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (IsEnabled(logLevel))
            {
                sink.Add(formatter(state, exception));
            }
        }
    }
}

[TestFixture]
public sealed class JsonOptionsDivergenceTests
{
    [Test]
    public void Compare_ReturnsNothing_WhenBothAreFrameworkDefaults()
    {
        var differences = JsonOptionsDivergence.Compare(
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.That(differences, Is.Empty);
    }

    [Test]
    public void Compare_ReturnsNothing_WhenTheSameConverterTypeIsOnBoth()
    {
        var mvc = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var http = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Distinct instances of the same type on purpose: two registrations of the same
        // converter produce the same wire format and must not be reported.
        mvc.Converters.Add(new UtcDateTimeJsonConverter());
        http.Converters.Add(new UtcDateTimeJsonConverter());

        Assert.That(JsonOptionsDivergence.Compare(mvc, http), Is.Empty);
    }

    [Test]
    public void Compare_ReportsConverterPresentOnlyOnMvc()
    {
        var mvc = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        mvc.Converters.Add(new UtcDateTimeJsonConverter());

        var differences = JsonOptionsDivergence.Compare(
            mvc,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.That(differences, Has.One.Contains("UtcDateTimeJsonConverter")
            .And.One.Contains("but not on ConfigureHttpJsonOptions"));
    }

    /// <summary>
    /// The direction the adoption report's own workaround creates: converters moved to
    /// <c>ConfigureHttpJsonOptions</c> to compensate for the old behaviour now leave MVC bare.
    /// </summary>
    [Test]
    public void Compare_ReportsConverterPresentOnlyOnHttp()
    {
        var http = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        http.Converters.Add(new UtcDateTimeJsonConverter());

        var differences = JsonOptionsDivergence.Compare(
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            http);

        Assert.That(differences, Has.One.Contains("UtcDateTimeJsonConverter")
            .And.One.Contains("but not on AddControllers().AddJsonOptions"));
    }

    [Test]
    public void Compare_ReportsNamingPolicyDifference()
    {
        var differences = JsonOptionsDivergence.Compare(
            new JsonSerializerOptions { PropertyNamingPolicy = null },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.That(differences, Has.One.Contains("PropertyNamingPolicy differs"));
    }

    [Test]
    public void Compare_ReportsIgnoreConditionDifference()
    {
        var differences = JsonOptionsDivergence.Compare(
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            },
            new JsonSerializerOptions());

        Assert.That(differences, Has.One.Contains("DefaultIgnoreCondition differs"));
    }

    /// <summary>
    /// Adoption report F-1, the loud half: an MVC application whose two option objects disagree
    /// is told once, at boot, instead of shipping two serialisations of the same DTO in silence.
    /// </summary>
    [Test]
    public void UseApiEnvelope_WarnsWhenAnMvcApplicationsOptionsDiverge()
    {
        var sink = new WarningSink();

        using (StartHost(sink, services =>
        {
            services.AddControllers();
            services.ConfigureHttpJsonOptions(json =>
                json.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter()));
        }))
        {
            Assert.That(
                sink.Warnings,
                Has.One.Contains("MVC and minimal-API JSON options diverge")
                    .And.One.Contains("UtcDateTimeJsonConverter"));
        }
    }

    [Test]
    public void UseApiEnvelope_IsSilentWhenAnMvcApplicationsOptionsAgree()
    {
        var sink = new WarningSink();

        using (StartHost(sink, services => services.AddControllers()
            .AddJsonOptions(json =>
                json.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter()))
            .Services
            .ConfigureHttpJsonOptions(json =>
                json.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter()))))
        {
            Assert.That(sink.Warnings, Is.Empty);
        }
    }

    /// <summary>
    /// A minimal-API-only host still resolves a default <c>Mvc.JsonOptions</c> from the options
    /// pattern. Comparing against it unconditionally would warn about a difference no request
    /// this application can serve could ever observe — a false positive that teaches people to
    /// ignore the warning.
    /// </summary>
    [Test]
    public void UseApiEnvelope_IsSilentForAMinimalApiOnlyHost()
    {
        var sink = new WarningSink();

        using (StartHost(sink, services => services.ConfigureHttpJsonOptions(json =>
            json.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter()))))
        {
            Assert.That(sink.Warnings, Is.Empty);
        }
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
                services.AddApiEnvelope();
            });
            web.Configure(app => app.UseApiEnvelope());
        }).Start();
    }
}
