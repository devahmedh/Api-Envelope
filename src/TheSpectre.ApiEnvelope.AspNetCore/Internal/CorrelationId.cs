using System.Diagnostics;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Resolves the correlation id carried on every response.</summary>
internal static class CorrelationId
{
    /// <summary>The <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> key.</summary>
    internal const string ItemsKey = "TheSpectre.ApiEnvelope.CorrelationId";

    private const int MaxLength = 128;

    /// <summary>
    /// Whether an inbound header value may be echoed.
    /// </summary>
    /// <remarks>
    /// Permits ASCII letters, digits, dot, underscore and hyphen, up to 128 characters. This is
    /// a security control: an unvalidated value reaches both a log line and a response header,
    /// where CR/LF would allow log injection and response splitting. Malformed values are
    /// discarded rather than sanitised.
    /// </remarks>
    internal static bool IsWellFormed(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the inbound value when well-formed, otherwise the current
    /// <see cref="Activity"/>'s W3C trace id, otherwise a generated value.
    /// </summary>
    /// <remarks>
    /// Preferring <see cref="Activity.TraceId"/> means the id in the envelope is the same
    /// string Application Insights and any OpenTelemetry backend index traces by, so a user's
    /// bug report resolves to one lookup in the system the traces already live in rather than
    /// a second identifier to join on.
    /// </remarks>
    internal static string Resolve(string? inboundHeaderValue)
    {
        if (IsWellFormed(inboundHeaderValue))
        {
            return inboundHeaderValue!;
        }

        var activity = Activity.Current;

        if (activity is not null && activity.IdFormat == ActivityIdFormat.W3C)
        {
            return activity.TraceId.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}
