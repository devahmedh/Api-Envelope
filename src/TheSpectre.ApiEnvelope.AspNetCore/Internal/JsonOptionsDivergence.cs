using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>
/// Warns at startup when an application's MVC and minimal-API serializer options disagree.
/// </summary>
/// <remarks>
/// ASP.NET Core has two unrelated JSON option objects: <c>AddControllers().AddJsonOptions(...)</c>
/// configures <see cref="Microsoft.AspNetCore.Mvc.JsonOptions"/>, and
/// <c>ConfigureHttpJsonOptions(...)</c> configures
/// <see cref="Microsoft.AspNetCore.Http.Json.JsonOptions"/>. Each pipeline reads its own, so an
/// application hosting controllers and minimal APIs side by side can serialise the same DTO two
/// different ways with a green build and no warning anywhere. A converter that pins
/// <see cref="DateTime"/> to UTC registered on only one of them is the concrete case: half the
/// API ships correct timestamps and the other half ships timestamps shifted by the server's
/// offset. Nothing throws. This check makes that divergence audible once, at boot.
/// </remarks>
internal static class JsonOptionsDivergence
{
    private const string LoggerCategory = "TheSpectre.ApiEnvelope.AspNetCore";

    /// <summary>Logs one warning if the two option objects differ observably.</summary>
    /// <param name="services">The application's service provider.</param>
    internal static void WarnIfDiverged(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Only meaningful when MVC is actually part of the application. The options pattern
        // hands back a default Mvc.JsonOptions instance whether or not AddControllers() ran, so
        // comparing unconditionally would warn a minimal-API-only host about a difference no
        // request it can serve could ever observe. IActionDescriptorCollectionProvider is
        // registered by AddMvcCore and by nothing else.
        if (services.GetService<IActionDescriptorCollectionProvider>() is null)
        {
            return;
        }

        var mvc = services
            .GetService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
            ?.Value.JsonSerializerOptions;

        var http = services
            .GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            ?.Value.SerializerOptions;

        if (mvc is null || http is null)
        {
            return;
        }

        var differences = Compare(mvc, http);

        if (differences.Count == 0)
        {
            return;
        }

        services.GetService<ILoggerFactory>()?.CreateLogger(LoggerCategory).LogWarning(
            "MVC and minimal-API JSON options diverge, so controllers and minimal-API endpoints " +
            "in this application will not serialise identically: {Differences}. " +
            "AddControllers().AddJsonOptions(...) configures " +
            "Microsoft.AspNetCore.Mvc.JsonOptions; ConfigureHttpJsonOptions(...) configures " +
            "Microsoft.AspNetCore.Http.Json.JsonOptions. Register the same settings on both, or " +
            "disregard this if the difference is deliberate.",
            string.Join("; ", differences));
    }

    /// <summary>Describes every observable difference between the two option objects.</summary>
    /// <param name="mvc">MVC's serializer options.</param>
    /// <param name="http">The minimal-API and middleware serializer options.</param>
    /// <remarks>
    /// Deliberately limited to the three settings that change what a client receives:
    /// converters, property naming and the null-ignore condition. Comparing every property of
    /// <see cref="JsonSerializerOptions"/> would report differences no consumer can act on and
    /// train people to ignore the warning.
    /// </remarks>
    internal static IReadOnlyList<string> Compare(
        JsonSerializerOptions mvc,
        JsonSerializerOptions http)
    {
        ArgumentNullException.ThrowIfNull(mvc);
        ArgumentNullException.ThrowIfNull(http);

        var differences = new List<string>();

        foreach (var name in ConverterTypeNamesMissingFrom(mvc, http))
        {
            differences.Add(
                $"converter '{name}' is registered on AddControllers().AddJsonOptions(...) " +
                "but not on ConfigureHttpJsonOptions(...)");
        }

        foreach (var name in ConverterTypeNamesMissingFrom(http, mvc))
        {
            differences.Add(
                $"converter '{name}' is registered on ConfigureHttpJsonOptions(...) " +
                "but not on AddControllers().AddJsonOptions(...)");
        }

        // Compared by type, not by reference: JsonNamingPolicy.CamelCase is a singleton, but a
        // consumer's own policy is a fresh instance per registration and two instances of the
        // same policy type produce the same wire format.
        if (mvc.PropertyNamingPolicy?.GetType() != http.PropertyNamingPolicy?.GetType())
        {
            differences.Add(
                $"PropertyNamingPolicy differs: MVC uses {Describe(mvc.PropertyNamingPolicy)}, " +
                $"minimal APIs use {Describe(http.PropertyNamingPolicy)}");
        }

        if (mvc.DefaultIgnoreCondition != http.DefaultIgnoreCondition)
        {
            differences.Add(
                $"DefaultIgnoreCondition differs: MVC uses {mvc.DefaultIgnoreCondition}, " +
                $"minimal APIs use {http.DefaultIgnoreCondition}");
        }

        return differences;
    }

    private static IEnumerable<string> ConverterTypeNamesMissingFrom(
        JsonSerializerOptions source,
        JsonSerializerOptions target)
    {
        var present = new HashSet<Type>();

        foreach (var converter in target.Converters)
        {
            present.Add(converter.GetType());
        }

        var reported = new HashSet<Type>();

        foreach (var converter in source.Converters)
        {
            var type = converter.GetType();

            if (!present.Contains(type) && reported.Add(type))
            {
                yield return type.Name;
            }
        }
    }

    private static string Describe(JsonNamingPolicy? policy) =>
        policy is null ? "no policy (PascalCase)" : policy.GetType().Name;
}
