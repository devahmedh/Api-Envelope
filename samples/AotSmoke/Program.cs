using System.Text.Json.Serialization;
using TheSpectre.ApiEnvelope;
using TheSpectre.ApiEnvelope.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

// Only Project is registered. ApiResponse<Project> deliberately is NOT: the library must
// resolve the payload's own contract and never need the wrapper declared. If that stopped
// being true, the first request below would throw NotSupportedException at runtime — which
// no analyser can predict and only this sample can catch.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AotJsonContext.Default));

builder.Services.AddApiEnvelope();

var app = builder.Build();

app.UseApiEnvelope();

var api = app.MapGroup("").WithApiEnvelope();

api.MapGet("/project", () => new Project(42, "Ahmed"));
api.MapGet("/conflict", void () => throw new AppException("PROJECT_CODE_TAKEN", statusCode: 409));

app.Run();

internal sealed record Project(int Id, string Name);

[JsonSerializable(typeof(Project))]
internal sealed partial class AotJsonContext : JsonSerializerContext;
