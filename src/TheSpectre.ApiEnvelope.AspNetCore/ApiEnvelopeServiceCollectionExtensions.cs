using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // TryAdd, not Add: the two-argument AddApiEnvelope(configure) overload below calls this
        // parameterless one, so a caller who uses that overload must not get two registrations.
        services.TryAddSingleton<EnvelopeLoggers>();

        // Strictly before the AddExceptionHandler call below: handlers run in registration
        // order and the first to return true wins, so anything already registered here silently
        // outranks this library's handler. UseApiEnvelope() turns what this finds into a warning.
        ExceptionHandlerOrder.Capture(services);

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

    /// <summary>
    /// Applies one <see cref="JsonSerializerOptions"/> configuration to both of ASP.NET Core's
    /// serializer option objects.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the serializer options.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <remarks>
    /// <para>
    /// Controllers read <c>Microsoft.AspNetCore.Mvc.JsonOptions</c>; minimal APIs, error
    /// envelopes and status-code envelopes read
    /// <c>Microsoft.AspNetCore.Http.Json.JsonOptions</c>. A converter registered on only one is
    /// silently absent from the other half of an application — the failure
    /// <c>UseApiEnvelope()</c> warns about at startup. This writes to both.
    /// </para>
    /// <para>
    /// This is a convenience over the framework's own two objects, not a third place to
    /// configure JSON. The envelope has no serializer options of its own: the payload is the
    /// caller's, and it must serialise the way their application serialises it everywhere else.
    /// </para>
    /// <para>
    /// In an application that never calls <c>AddControllers()</c>, the MVC registration is inert
    /// — nothing resolves those options — so no check for MVC is needed or made.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddApiEnvelopeJson(
        this IServiceCollection services,
        Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(
            options => configure(options.JsonSerializerOptions));

        services.ConfigureHttpJsonOptions(
            options => configure(options.SerializerOptions));

        return services;
    }
}
