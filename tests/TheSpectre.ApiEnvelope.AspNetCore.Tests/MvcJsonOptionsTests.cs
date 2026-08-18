using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.AspNetCore.Tests;

/// <summary>Forces a <see cref="DateTime"/> to a UTC-anchored string, marker suffix and all.</summary>
/// <remarks>
/// Stands in for the four UTC converters the adoption report described. The <c>Z</c> suffix is
/// the entire assertion: without the converter, System.Text.Json writes an offset-less
/// <c>2026-08-18T09:30:00</c> that a browser at UTC+3 parses as local time.
/// </remarks>
internal sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => reader.GetDateTime();

    public override void Write(
        Utf8JsonWriter writer,
        DateTime value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture));
}

[ApiController]
[Route("json-options")]
public sealed class JsonOptionsTestController : ControllerBase
{
    public sealed record Reading(DateTime Timestamp);

    internal static readonly DateTime Sample =
        new(2026, 8, 18, 9, 30, 0, DateTimeKind.Unspecified);

    [HttpGet("reading")]
    public IActionResult GetReading() => Ok(new Reading(Sample));
}

/// <summary>
/// Which <c>JsonSerializerOptions</c> object each pipeline serialises the envelope's
/// <c>result</c> with.
/// </summary>
[TestFixture]
public sealed class MvcJsonOptionsTests
{
    private const string Unconverted = "2026-08-18T09:30:00";
    private const string Converted = "2026-08-18T09:30:00Z";

    /// <summary>
    /// Adoption report F-1: a converter registered the documented MVC way must reach the
    /// envelope. Writing the envelope bypasses MVC's output formatters, so before the fix the
    /// converter was skipped and every timestamp shipped without its UTC anchor — silently,
    /// with a green build.
    /// </summary>
    [Test]
    public async Task MvcResult_UsesConverterRegisteredWithAddJsonOptions()
    {
        using var host = CreateMvcHost(mvc =>
            mvc.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter()));

        var timestamp = await ReadTimestampAsync(host, "/json-options/reading");

        Assert.That(timestamp, Is.EqualTo(Converted));
    }

    [Test]
    public async Task MvcResult_WithoutConverter_WritesTheFrameworkDefault()
    {
        using var host = CreateMvcHost(static _ => { });

        var timestamp = await ReadTimestampAsync(host, "/json-options/reading");

        Assert.That(timestamp, Is.EqualTo(Unconverted));
    }

    /// <summary>
    /// The other half of the contract: minimal APIs keep reading
    /// <c>Http.Json.JsonOptions</c>, so routing MVC to its own options must not move them.
    /// </summary>
    [Test]
    public async Task MinimalApi_UsesConverterRegisteredWithConfigureHttpJsonOptions()
    {
        using var host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.ConfigureHttpJsonOptions(json =>
                    json.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter()));
                services.AddApiEnvelope();
                services.AddRouting();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints
                    .MapGet("/reading",
                        () => new JsonOptionsTestController.Reading(
                            JsonOptionsTestController.Sample))
                    .WithApiEnvelope());
            });
        }).Start();

        var timestamp = await ReadTimestampAsync(host, "/reading");

        Assert.That(timestamp, Is.EqualTo(Converted));
    }

    private static IHost CreateMvcHost(Action<Microsoft.AspNetCore.Mvc.JsonOptions> configure)
    {
        return new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(JsonOptionsTestController).Assembly)
                    .AddJsonOptions(configure);
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

    private static async Task<string?> ReadTimestampAsync(IHost host, string path)
    {
        var response = await host.GetTestClient().GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        return JsonDocument.Parse(body).RootElement
            .GetProperty("result")
            .GetProperty("timestamp")
            .GetString();
    }
}
