using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.AspNetCore;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Tests;

/// <summary>The DataAnnotations side of the parity suite: same route, same two rules.</summary>
[TestFixture]
public sealed class DataAnnotationsParityTests : ValidationParityTests
{
    public sealed class CreateProject
    {
        [Required(ErrorMessage = ValidationErrorCodes.Required)]
        public string? Code { get; set; }

        [StringLength(5, ErrorMessage = ValidationErrorCodes.TooLong)]
        public string? Title { get; set; }
    }

    [ApiController]
    [Route("projects")]
    public sealed class ProjectsController : ControllerBase
    {
        [HttpPost]
        public IActionResult Create(CreateProject model) => Ok(model);
    }

    protected override HttpClient CreateClient()
    {
        var host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.UseEnvironment("Production");
            web.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(ProjectsController).Assembly);
                services.AddApiEnvelope();
                services.AddApiEnvelopeDataAnnotations();
            });
            web.Configure(app =>
            {
                app.UseApiEnvelope();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        }).Start();

        return host.GetTestClient();
    }
}
