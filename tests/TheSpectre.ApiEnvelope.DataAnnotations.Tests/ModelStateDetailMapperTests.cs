using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using TheSpectre.ApiEnvelope.DataAnnotations.Internal;
using RangeAttribute = System.ComponentModel.DataAnnotations.RangeAttribute;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Tests;

[TestFixture]
public sealed class ModelStateDetailMapperTests
{
    private static readonly JsonNamingPolicy Camel = JsonNamingPolicy.CamelCase;

    private sealed class Project
    {
        [Required(ErrorMessage = ValidationErrorCodes.Required)]
        [StringLength(400, ErrorMessage = ValidationErrorCodes.TooLong)]
        public string? Title { get; set; }

        [Range(1, 10)]
        public int Priority { get; set; }

        [Required]
        public string? Code { get; set; }
    }

    // EmptyModelMetadataProvider (as its name states) never populates ValidatorMetadata, so it
    // cannot exercise the attribute lookup this mapper depends on. Building the same MVC
    // services a real host would (AddMvcCore + AddDataAnnotations) yields a provider whose
    // ValidatorMetadata is populated exactly as it is in production.
    private static readonly IModelMetadataProvider MetadataProvider = BuildMetadataProvider();

    private static IModelMetadataProvider BuildMetadataProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddDataAnnotations();
        return services.BuildServiceProvider().GetRequiredService<IModelMetadataProvider>();
    }

    private static ModelMetadata MetadataFor<T>() => MetadataProvider.GetMetadataForType(typeof(T));

    [Test]
    public void Map_WhenErrorMessageIsAKey_UsesItAndTakesParamsFromTheMatchingAttribute()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Title", ValidationErrorCodes.TooLong);

        var details = ModelStateDetailMapper.Map(modelState, MetadataFor<Project>(), Camel);

        Assert.That(details, Has.Count.EqualTo(1));
        Assert.That(details[0].Field, Is.EqualTo("title"));
        Assert.That(details[0].ErrorCode, Is.EqualTo(ValidationErrorCodes.TooLong));
        Assert.That(details[0].Params!["max"], Is.EqualTo(400));
    }

    [Test]
    public void Map_WhenErrorMessageIsAKey_DistinguishesBetweenTwoAttributesOnOneProperty()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Title", ValidationErrorCodes.Required);

        var details = ModelStateDetailMapper.Map(modelState, MetadataFor<Project>(), Camel);

        Assert.That(details[0].ErrorCode, Is.EqualTo(ValidationErrorCodes.Required));
        Assert.That(details[0].Params, Is.Null);
    }

    [Test]
    public void Map_WithEnglishProseAndASingleAttribute_InfersFromTheAttribute()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Priority", "The field Priority must be between 1 and 10.");

        var details = ModelStateDetailMapper.Map(modelState, MetadataFor<Project>(), Camel);

        Assert.That(details[0].ErrorCode, Is.EqualTo(ValidationErrorCodes.OutOfRange));
        Assert.That(details[0].Params!["min"], Is.EqualTo(1));
        Assert.That(details[0].Params!["max"], Is.EqualTo(10));
    }

    [Test]
    public void Map_WithEnglishProseAndMultipleAttributes_FallsBackToInvalidFormat()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Title", "The Title field is required.");

        var details = ModelStateDetailMapper.Map(modelState, MetadataFor<Project>(), Camel);

        Assert.That(details[0].ErrorCode, Is.EqualTo(ValidationErrorCodes.InvalidFormat));
        Assert.That(details[0].Params, Is.Null);
    }

    [Test]
    public void Map_NeverEmitsTheEnglishErrorMessage()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Title", "The Title field is required.");
        modelState.AddModelError("Priority", "must be between 1 and 10");

        var details = ModelStateDetailMapper.Map(modelState, MetadataFor<Project>(), Camel);

        foreach (var detail in details)
        {
            Assert.That(detail.ErrorCode, Does.Match("^[A-Z][A-Z0-9_]*$"));
            Assert.That(detail.ErrorCode, Does.Not.Contain(" "));
        }
    }

    [Test]
    public void Map_ConvertsPropertyNamesToJsonFieldPaths()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Code", ValidationErrorCodes.Required);

        var details = ModelStateDetailMapper.Map(modelState, MetadataFor<Project>(), Camel);

        Assert.That(details[0].Field, Is.EqualTo("code"));
    }

    [Test]
    public void Map_WithNoErrors_ReturnsEmpty()
    {
        Assert.That(
            ModelStateDetailMapper.Map(new ModelStateDictionary(), MetadataFor<Project>(), Camel),
            Is.Empty);
    }

    [Test]
    public void Map_WithNoMetadata_StillProducesFieldAndAKey()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Title", ValidationErrorCodes.TooLong);

        var details = ModelStateDetailMapper.Map(modelState, modelMetadata: null, Camel);

        Assert.That(details[0].Field, Is.EqualTo("title"));
        Assert.That(details[0].ErrorCode, Is.EqualTo(ValidationErrorCodes.TooLong));
        Assert.That(details[0].Params, Is.Null);
    }
}
