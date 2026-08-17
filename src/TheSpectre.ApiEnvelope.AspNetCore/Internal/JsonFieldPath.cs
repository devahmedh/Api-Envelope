using System.Text;
using System.Text.Json;

namespace TheSpectre.ApiEnvelope.AspNetCore.Internal;

/// <summary>Converts a CLR property path into the JSON path a client sees.</summary>
/// <remarks>
/// Validation libraries report failures against CLR names (<c>Address.City</c>), but the
/// client matches <c>field</c> against its own form controls, which are named after the JSON
/// it received (<c>address.city</c>). Both validation integrations route through here, so the
/// two cannot disagree on what a field is called.
/// </remarks>
internal static class JsonFieldPath
{
    /// <summary>Converts <paramref name="clrPath"/> using <paramref name="namingPolicy"/>.</summary>
    internal static string FromClrPath(string clrPath, JsonNamingPolicy? namingPolicy)
    {
        if (string.IsNullOrEmpty(clrPath) || namingPolicy is null)
        {
            return clrPath;
        }

        var builder = new StringBuilder(clrPath.Length);
        var segments = clrPath.Split('.');

        for (var i = 0; i < segments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('.');
            }

            var segment = segments[i];
            var bracket = segment.IndexOf('[', StringComparison.Ordinal);

            if (bracket < 0)
            {
                builder.Append(namingPolicy.ConvertName(segment));
            }
            else
            {
                // Convert the name, leave the indexer byte-for-byte: "Items[0]" -> "items[0]".
                builder.Append(namingPolicy.ConvertName(segment[..bracket]));
                builder.Append(segment[bracket..]);
            }
        }

        return builder.ToString();
    }
}
