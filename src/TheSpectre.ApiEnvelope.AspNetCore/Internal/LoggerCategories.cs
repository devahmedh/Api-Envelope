namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>The logger categories this library writes under.</summary>
/// <remarks>
/// Hand-written and stable, deliberately not <c>ILogger&lt;SomeInternalType&gt;</c>. A category
/// is a configuration key an operator writes in <c>appsettings.json</c>; deriving it from an
/// internal type name means renaming that type silently breaks their filter.
/// </remarks>
internal static class LoggerCategories
{
    /// <summary>A validation failure — an error envelope carrying field-level details.</summary>
    internal const string Validation = "TheSpectre.ApiEnvelope.Validation";

    /// <summary>An error envelope with no details: a routing 404, an authentication 401.</summary>
    internal const string StatusCode = "TheSpectre.ApiEnvelope.StatusCode";

    /// <summary>An unhandled exception, or any application exception that is not a validation failure.</summary>
    internal const string Exception = "TheSpectre.ApiEnvelope.Exception";
}
