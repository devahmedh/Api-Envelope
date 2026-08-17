using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.Tests;

[TestFixture]
public sealed class ApiEnvelopeWriterTests
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private sealed record Sample(int Id, string Name);

    [Test]
    public void WriteToUtf8Bytes_ForSuccessWithPayload_MatchesGolden()
    {
        var response = ApiResponse.Success(new Sample(42, "Ahmed"), TraceId);

        GoldenFile.Assert("success-200.json",
            ApiEnvelopeWriter.WriteToUtf8Bytes(response, WebOptions));
    }

    [Test]
    public void WriteToUtf8Bytes_ForSuccessWithNoContent_MatchesGolden()
    {
        var response = ApiResponse.Success<Sample?>(null, TraceId, 204);

        GoldenFile.Assert("success-204.json",
            ApiEnvelopeWriter.WriteToUtf8Bytes(response, WebOptions));
    }

    [Test]
    public void WriteToUtf8Bytes_ForConflict_MatchesGolden()
    {
        var response = ApiResponse.Failure(
            "PROJECT_CODE_TAKEN", TraceId, 409,
            "Project code PERMIT already exists in this tenant.");

        GoldenFile.Assert("error-409.json",
            ApiEnvelopeWriter.WriteToUtf8Bytes(response, WebOptions));
    }

    [Test]
    public void WriteToUtf8Bytes_ForInternalErrorInProduction_OmitsMessage()
    {
        var response = ApiResponse.Failure(ErrorCodes.InternalError, TraceId, 500);

        GoldenFile.Assert("error-500-production.json",
            ApiEnvelopeWriter.WriteToUtf8Bytes(response, WebOptions));
    }

    [Test]
    public void WriteToUtf8Bytes_ForInternalErrorInDevelopment_IncludesMessage()
    {
        var response = ApiResponse.Failure(
            ErrorCodes.InternalError, TraceId, 500,
            "Object reference not set to an instance of an object.");

        GoldenFile.Assert("error-500-development.json",
            ApiEnvelopeWriter.WriteToUtf8Bytes(response, WebOptions));
    }

    [Test]
    public void WriteToUtf8Bytes_ForValidationFailure_MatchesGolden()
    {
        var details = new[]
        {
            new ErrorDetail("title", ValidationErrorCodes.TooLong,
                new Dictionary<string, object?> { ["max"] = 400 }),
            new ErrorDetail("code", ValidationErrorCodes.Required),
        };

        var response = ApiResponse.Failure(
            ErrorCodes.ValidationFailed, TraceId, 400, details: details);

        GoldenFile.Assert("validation-400.json",
            ApiEnvelopeWriter.WriteToUtf8Bytes(response, WebOptions));
    }

    [Test]
    public void WriteToUtf8Bytes_ForUnauthorized_MatchesGolden()
    {
        var response = ApiResponse.Failure(ErrorCodes.Unauthorized, TraceId, 401);

        GoldenFile.Assert("unauthorized-401.json",
            ApiEnvelopeWriter.WriteToUtf8Bytes(response, WebOptions));
    }

    [Test]
    public void WriteToUtf8Bytes_ForNotFound_MatchesGolden()
    {
        var response = ApiResponse.Failure(ErrorCodes.NotFound, TraceId, 404);

        GoldenFile.Assert("notfound-404.json",
            ApiEnvelopeWriter.WriteToUtf8Bytes(response, WebOptions));
    }

    [Test]
    public void WriteToUtf8Bytes_ForSuccessWithPagedResult_MatchesGolden()
    {
        var page = new PagedResult<Sample>(
            new[] { new Sample(3, "C"), new Sample(4, "D") },
            new PaginationData(2, 2, 57));

        var response = ApiResponse.Success(page, TraceId);

        GoldenFile.Assert("success-paged-200.json",
            ApiEnvelopeWriter.WriteToUtf8Bytes(response, WebOptions));
    }

    [Test]
    public void WriteToUtf8Bytes_IgnoresHostNamingPolicyForEnvelopeProperties()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };

        var json = Encoding.UTF8.GetString(
            ApiEnvelopeWriter.WriteToUtf8Bytes(
                ApiResponse.Success(new Sample(1, "x"), TraceId), options));

        Assert.That(json, Does.StartWith("{\"isSuccess\":true,\"statusCode\":200,\"result\":"));
    }

    [Test]
    public void WriteToUtf8Bytes_UsesHostSerializerOptionsForThePayload()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };

        var json = Encoding.UTF8.GetString(
            ApiEnvelopeWriter.WriteToUtf8Bytes(
                ApiResponse.Success(new Sample(1, "x"), TraceId), options));

        // Envelope names are fixed; the payload obeys the host's policy (PascalCase here).
        Assert.That(json, Does.Contain("\"Id\":1"));
        Assert.That(json, Does.Contain("\"Name\":\"x\""));
    }

    [Test]
    public void WriteToUtf8Bytes_WritesPropertiesInContractOrder()
    {
        var json = Encoding.UTF8.GetString(
            ApiEnvelopeWriter.WriteToUtf8Bytes(
                ApiResponse.Failure("X", TraceId, 400, "m",
                    new[] { new ErrorDetail("f", "C") }),
                WebOptions));

        var order = new[]
        {
            "\"isSuccess\"", "\"statusCode\"", "\"result\"", "\"errorCode\"",
            "\"message\"", "\"correlationId\"", "\"details\"",
        };

        var positions = order.Select(name => json.IndexOf(name)).ToArray();

        Assert.That(positions, Is.All.GreaterThanOrEqualTo(0));
        Assert.That(positions, Is.Ordered.Ascending);
    }

    [Test]
    public void WriteToUtf8Bytes_ForEveryPermittedParamValueType_WritesExpectedJsonLiterals()
    {
        var detail = new ErrorDetail("field", "CODE", new Dictionary<string, object?>
        {
            ["text"] = "abc",
            ["int"] = 400,
            ["dbl"] = 1.5d,
            ["flag"] = true,
            ["nothing"] = null,
        });

        var json = Encoding.UTF8.GetString(ApiEnvelopeWriter.WriteToUtf8Bytes(
            ApiResponse.Failure(ErrorCodes.ValidationFailed, TraceId, 400, details: new[] { detail }),
            WebOptions));

        Assert.That(json, Does.Contain("\"text\":\"abc\""));
        Assert.That(json, Does.Contain("\"int\":400"));
        Assert.That(json, Does.Contain("\"dbl\":1.5"));
        Assert.That(json, Does.Contain("\"flag\":true"));
        Assert.That(json, Does.Contain("\"nothing\":null"));
    }
}
