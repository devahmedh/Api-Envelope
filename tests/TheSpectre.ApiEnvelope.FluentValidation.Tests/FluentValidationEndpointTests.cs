using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore;

namespace TheSpectre.ApiEnvelope.FluentValidation.Tests;

[TestFixture]
public sealed class FluentValidationEndpointTests
{
    public sealed record CreateProject(string Title, string Code);

    public sealed class CreateProjectValidator : AbstractValidator<CreateProject>
    {
        public CreateProjectValidator()
        {
            RuleFor(p => p.Code).NotEmpty().WithErrorCode(ValidationErrorCodes.Required);
            RuleFor(p => p.Title).MaximumLength(5).WithErrorCode(ValidationErrorCodes.TooLong);
        }
    }

    private static IHost CreateHost() =>
        new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddApiEnvelope();
                services.AddSingleton<IValidator<CreateProject>, CreateProjectValidator>();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    var api = endpoints.MapGroup("").WithApiEnvelope();
                    api.MapPost("/projects", (CreateProject model) => model)
                       .WithFluentValidation<RouteHandlerBuilder, CreateProject>();
                });
            });
        }).Start();

    [Test]
    public async Task InvalidModel_ProducesValidationFailedWithPerFieldKeys()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient()
            .PostAsJsonAsync("/projects", new CreateProject("far too long", ""));
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(body).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo(ErrorCodes.ValidationFailed));

        var details = envelope.GetProperty("details");
        Assert.That(details.GetArrayLength(), Is.EqualTo(2));

        var fields = details.EnumerateArray().Select(d => d.GetProperty("field").GetString()).ToArray();
        Assert.That(fields, Is.EquivalentTo(new[] { "code", "title" }));
    }

    [Test]
    public async Task InvalidModel_NeverPutsAnEnglishSentenceOnTheWire()
    {
        using var host = CreateHost();

        var body = await (await host.GetTestClient()
            .PostAsJsonAsync("/projects", new CreateProject("far too long", ""))).Content.ReadAsStringAsync();

        Assert.That(body, Does.Not.Contain("must not be empty"));
        Assert.That(body, Does.Not.Contain("characters or fewer"));
        Assert.That(body, Does.Not.Contain("'Code'"));
    }

    [Test]
    public async Task InvalidModel_CarriesTheBoundAsAParam()
    {
        using var host = CreateHost();

        var envelope = JsonDocument.Parse(await (await host.GetTestClient()
            .PostAsJsonAsync("/projects", new CreateProject("far too long", "OK")))
            .Content.ReadAsStringAsync()).RootElement;

        var titleDetail = envelope.GetProperty("details").EnumerateArray()
            .Single(d => d.GetProperty("field").GetString() == "title");

        Assert.That(titleDetail.GetProperty("errorCode").GetString(), Is.EqualTo(ValidationErrorCodes.TooLong));

        var titleParams = titleDetail.GetProperty("params");
        Assert.That(titleParams.EnumerateObject().Count(), Is.EqualTo(1));
        Assert.That(titleParams.GetProperty("max").GetInt32(), Is.EqualTo(5));
    }

    [Test]
    public async Task ValidModel_PassesThroughAndIsEnveloped()
    {
        using var host = CreateHost();

        var response = await host.GetTestClient()
            .PostAsJsonAsync("/projects", new CreateProject("ok", "C1"));
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.True);
        Assert.That(envelope.GetProperty("result").GetProperty("title").GetString(), Is.EqualTo("ok"));
    }
}
