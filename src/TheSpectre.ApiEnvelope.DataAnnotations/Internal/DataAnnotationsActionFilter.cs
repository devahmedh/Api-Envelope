using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Internal;

/// <summary>Short-circuits an invalid model with a <c>VALIDATION_FAILED</c> envelope.</summary>
internal sealed class DataAnnotationsActionFilter : IAsyncActionFilter
{
    private readonly IModelMetadataProvider _metadataProvider;
    private readonly IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> _json;

    public DataAnnotationsActionFilter(
        IModelMetadataProvider metadataProvider,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json)
    {
        _metadataProvider = metadataProvider;
        _json = json;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.ModelState.IsValid)
        {
            await next();
            return;
        }

        // The action's first parameter is the model to validate. A miss (no parameters, or a
        // parameter type the provider cannot describe) degrades to null metadata, which the
        // mapper turns into the generic key rather than crashing.
        var modelType = context.ActionDescriptor.Parameters
            .OfType<ControllerParameterDescriptor>()
            .FirstOrDefault()?.ParameterType;

        var metadata = modelType is not null
            ? _metadataProvider.GetMetadataForType(modelType)
            : null;

        // Thrown rather than written: the exception handler already renders exactly this
        // envelope, so one place decides what a validation failure looks like.
        throw new AppException(
            ErrorCodes.ValidationFailed,
            StatusCodes.Status400BadRequest,
            ModelStateDetailMapper.Map(
                context.ModelState,
                metadata,
                _json.Value.SerializerOptions.PropertyNamingPolicy));
    }
}
