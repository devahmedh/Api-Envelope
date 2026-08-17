using Microsoft.Extensions.DependencyInjection;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.AspNetCore;

/// <summary>Registration for the API envelope.</summary>
public static class ApiEnvelopeServiceCollectionExtensions
{
    /// <summary>Registers the API envelope with default options.</summary>
    /// <param name="services">The service collection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddApiEnvelope(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<ApiEnvelopeOptions>();
        services.AddExceptionHandler<ApiEnvelopeExceptionHandler>();

        // MVC filters register application-wide, so controllers need no per-endpoint opt-in.
        // Configure rather than AddControllers: this must not force MVC on a minimal-API app.
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(mvc =>
        {
            mvc.Filters.Add<ApiEnvelopeResultFilter>();
            mvc.Conventions.Add(new EnvelopeApiResponseConvention());
        });

        return services;
    }

    /// <summary>Registers the API envelope and configures its options.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the options.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static IServiceCollection AddApiEnvelope(
        this IServiceCollection services,
        Action<ApiEnvelopeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddApiEnvelope();
        services.Configure(configure);

        return services;
    }
}
