using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore;
using TheSpectre.ApiEnvelope.FluentValidation;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Tests;

/// <summary>The FluentValidation side of the parity suite: same route, same two rules.</summary>
[TestFixture]
public sealed class FluentValidationParityTests : ValidationParityTests
{
    public sealed record CreateProject(string? Code, string? Title);

    public sealed class CreateProjectValidator : AbstractValidator<CreateProject>
    {
        public CreateProjectValidator()
        {
            RuleFor(p => p.Code).NotEmpty().WithErrorCode(ValidationErrorCodes.Required);
            RuleFor(p => p.Title).MaximumLength(5).WithErrorCode(ValidationErrorCodes.TooLong);
        }
    }

    protected override HttpClient CreateClient()
    {
        var host = new HostBuilder().ConfigureWebHost(web =>
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

        return host.GetTestClient();
    }
}
