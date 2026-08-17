using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace TheSpectre.ApiEnvelope.FluentValidation.Internal;

/// <summary>Runs the registered validator for <typeparamref name="TModel"/> before the handler.</summary>
/// <typeparam name="TModel">The model to validate.</typeparam>
internal sealed class FluentValidationEndpointFilter<TModel> : IEndpointFilter
    where TModel : class
{
    private readonly IValidator<TModel>? _validator;
    private readonly IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> _json;

    public FluentValidationEndpointFilter(
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json,
        IValidator<TModel>? validator = null)
    {
        _json = json;
        _validator = validator;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (_validator is null)
        {
            return await next(context);
        }

        var model = context.Arguments.OfType<TModel>().FirstOrDefault();

        if (model is null)
        {
            return await next(context);
        }

        var result = await _validator.ValidateAsync(model, context.HttpContext.RequestAborted);

        if (result.IsValid)
        {
            return await next(context);
        }

        // Thrown rather than written: the exception handler already renders exactly this
        // envelope, so there is one place that decides what a validation failure looks like.
        throw new AppException(
            ErrorCodes.ValidationFailed,
            StatusCodes.Status400BadRequest,
            result.ToErrorDetails(_json.Value.SerializerOptions.PropertyNamingPolicy));
    }
}
