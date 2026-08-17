using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TheSpectre.ApiEnvelope.DataAnnotations.Internal;

namespace TheSpectre.ApiEnvelope.DataAnnotations;

/// <summary>DataAnnotations integration for the API envelope.</summary>
public static class DataAnnotationsApiEnvelopeExtensions
{
    /// <summary>
    /// Replaces MVC's automatic model-validation response with a <c>VALIDATION_FAILED</c>
    /// envelope carrying per-field error keys.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <remarks>
    /// Suppresses <see cref="ApiBehaviorOptions.SuppressModelStateInvalidFilter"/>'s default
    /// filter, because <c>[ApiController]</c> otherwise short-circuits with a
    /// <c>ValidationProblemDetails</c> body — English prose composed on the server, which is
    /// exactly what this library exists to prevent reaching a user.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddApiEnvelopeDataAnnotations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
        services.Configure<MvcOptions>(o => o.Filters.Add<DataAnnotationsActionFilter>());

        return services;
    }
}
