using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using TheSpectre.ApiEnvelope.AspNetCore.Internal;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Internal;

/// <summary>Turns MVC's <see cref="ModelStateDictionary"/> into envelope details.</summary>
internal static partial class ModelStateDetailMapper
{
    /// <summary>Maps every model-state error to an <see cref="ErrorDetail"/>.</summary>
    internal static IReadOnlyList<ErrorDetail> Map(
        ModelStateDictionary modelState,
        ModelMetadata? modelMetadata,
        JsonNamingPolicy? namingPolicy)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        var details = new List<ErrorDetail>();

        foreach (var entry in modelState)
        {
            if (entry.Value.Errors.Count == 0)
            {
                continue;
            }

            var attributes = FindAttributes(modelMetadata, entry.Key);
            var field = JsonFieldPath.FromClrPath(entry.Key, namingPolicy);

            foreach (var error in entry.Value.Errors)
            {
                var (errorCode, parameters) = Resolve(error.ErrorMessage, attributes);
                details.Add(new ErrorDetail(field, errorCode, parameters));
            }
        }

        return details;
    }

    private static (string ErrorCode, IReadOnlyDictionary<string, object?>? Parameters) Resolve(
        string? errorMessage,
        IReadOnlyList<ValidationAttribute> attributes)
    {
        // 1. The ErrorMessage is itself the key. It also tells us WHICH attribute failed, which
        //    is the only reliable way to disambiguate a property carrying several of them.
        if (errorMessage is not null && KeyPattern().IsMatch(errorMessage))
        {
            foreach (var attribute in attributes)
            {
                if (AttributeCodeMap.TryMap(attribute, out var mappedCode, out var mappedParams)
                    && string.Equals(mappedCode, errorMessage, StringComparison.Ordinal))
                {
                    return (errorMessage, mappedParams);
                }
            }

            return (errorMessage, null);
        }

        // 2. English prose, but exactly one recognised attribute — infer it unambiguously.
        var recognised = new List<(string Code, IReadOnlyDictionary<string, object?>? Parameters)>();

        foreach (var attribute in attributes)
        {
            if (AttributeCodeMap.TryMap(attribute, out var code, out var parameters))
            {
                recognised.Add((code, parameters));
            }
        }

        if (recognised.Count == 1)
        {
            return recognised[0];
        }

        // 3. Ambiguous. Emit a generic key and NEVER the English sentence — putting server-authored
        //    prose on the wire is the exact failure this library exists to prevent.
        return (ValidationErrorCodes.InvalidFormat, null);
    }

    private static IReadOnlyList<ValidationAttribute> FindAttributes(
        ModelMetadata? modelMetadata,
        string propertyPath)
    {
        if (modelMetadata is null)
        {
            return [];
        }

        // Only the leaf segment is looked up: nested paths are rare in DataAnnotations models
        // and a miss simply falls through to the generic key rather than guessing.
        var leaf = propertyPath.Split('.')[^1];
        var property = modelMetadata.Properties.FirstOrDefault(
            p => string.Equals(p.PropertyName, leaf, StringComparison.Ordinal));

        if (property is null)
        {
            return [];
        }

        var attributes = new List<ValidationAttribute>();

        foreach (var metadata in property.ValidatorMetadata)
        {
            if (metadata is ValidationAttribute attribute)
            {
                attributes.Add(attribute);
            }
        }

        return attributes;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
