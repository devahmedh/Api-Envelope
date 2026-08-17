using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TheSpectre.ApiEnvelope.FluentValidation.Internal;

namespace TheSpectre.ApiEnvelope.FluentValidation;

/// <summary>FluentValidation integration for the API envelope.</summary>
public static class FluentValidationApiEnvelopeExtensions
{
    /// <summary>
    /// Validates <typeparamref name="TModel"/> with the registered
    /// <see cref="global::FluentValidation.IValidator{T}"/> before the handler runs, producing a
    /// <c>VALIDATION_FAILED</c> envelope with per-field keys when it fails.
    /// </summary>
    /// <typeparam name="TBuilder">The convention builder type.</typeparam>
    /// <typeparam name="TModel">The model to validate.</typeparam>
    /// <param name="builder">The endpoint or group to cover.</param>
    /// <remarks>
    /// If no validator is registered for <typeparamref name="TModel"/>, the endpoint runs
    /// unchanged — a missing validator is a configuration gap, not a request failure.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static TBuilder WithFluentValidation<TBuilder, TModel>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddEndpointFilter<TBuilder, FluentValidationEndpointFilter<TModel>>();

        return builder;
    }
}
