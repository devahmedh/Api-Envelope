using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.Tests;

[TestFixture]
public sealed class ApiResponseTests
{
    private sealed record Sample(int Id, string Name);

    [Test]
    public void Success_SetsSuccessFieldsAndLeavesErrorFieldsNull()
    {
        var response = ApiResponse.Success(new Sample(42, "Ahmed"), "trace-1");

        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.StatusCode, Is.EqualTo(200));
        Assert.That(response.Data, Is.EqualTo(new Sample(42, "Ahmed")));
        Assert.That(response.ErrorCode, Is.Null);
        Assert.That(response.Message, Is.Null);
        Assert.That(response.Details, Is.Null);
        Assert.That(response.CorrelationId, Is.EqualTo("trace-1"));
    }

    [Test]
    public void Failure_SetsErrorFieldsAndLeavesDataNull()
    {
        var details = new[] { new ErrorDetail("title", "REQUIRED") };

        var response = ApiResponse.Failure("VALIDATION_FAILED", "trace-2", 400, "diagnostic", details);

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.StatusCode, Is.EqualTo(400));
        Assert.That(response.Data, Is.Null);
        Assert.That(response.ErrorCode, Is.EqualTo("VALIDATION_FAILED"));
        Assert.That(response.Message, Is.EqualTo("diagnostic"));
        Assert.That(response.Details, Is.EqualTo(details));
    }

    [Test]
    public void ApiResponse_ImplementsIApiResponse()
    {
        IApiResponse response = ApiResponse.Success(1, "trace-3", 201);

        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.StatusCode, Is.EqualTo(201));
        Assert.That(response.ErrorCode, Is.Null);
        Assert.That(response.CorrelationId, Is.EqualTo("trace-3"));
    }

    [Test]
    public void Serialize_WithHostileNamingPolicy_StillEmitsCamelCaseContractNames()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper,
        };

        var json = JsonSerializer.Serialize(ApiResponse.Success(1, "trace-4"), options);

        Assert.That(json, Does.Contain("\"isSuccess\""));
        Assert.That(json, Does.Contain("\"correlationId\""));
        Assert.That(json, Does.Not.Contain("IS_SUCCESS"));
    }

    [Test]
    public void Serialize_WithDefaultIgnoreConditionWhenWritingNull_StillEmitsDataAndErrorCode()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        var json = JsonSerializer.Serialize(
            ApiResponse.Failure("CONFLICT", "trace-5", 409), options);

        Assert.That(json, Does.Contain("\"data\":null"));
        Assert.That(json, Does.Contain("\"errorCode\":\"CONFLICT\""));
    }

    [Test]
    public void Serialize_WithDefaultIgnoreConditionWhenWritingDefault_StillEmitsIsSuccessFalse()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };

        var json = JsonSerializer.Serialize(
            ApiResponse.Failure("CONFLICT", "trace-6", 409), options);

        Assert.That(json, Does.Contain("\"isSuccess\":false"));
    }

    [Test]
    public void Serialize_OnSuccess_OmitsMessageAndDetails()
    {
        var json = JsonSerializer.Serialize(ApiResponse.Success(1, "trace-7"));

        Assert.That(json, Does.Not.Contain("\"message\""));
        Assert.That(json, Does.Not.Contain("\"details\""));
    }
}
