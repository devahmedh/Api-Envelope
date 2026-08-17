using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.DataAnnotations.Tests;

/// <summary>
/// One set of assertions, two integrations. Written once so the two cannot drift.
/// </summary>
public abstract class ValidationParityTests
{
    /// <summary>Creates a host whose POST /projects validates a model with:
    /// a required Code, and a Title with a maximum length of 5.</summary>
    protected abstract HttpClient CreateClient();

    private async Task<JsonElement> PostAsync(object body)
    {
        var response = await CreateClient().PostAsJsonAsync("/projects", body);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Test]
    public async Task MissingRequiredField_ProducesRequiredWithNoParams()
    {
        var detail = (await PostAsync(new { title = "ok" }))
            .GetProperty("details").EnumerateArray()
            .Single(d => d.GetProperty("field").GetString() == "code");

        Assert.That(detail.GetProperty("errorCode").GetString(), Is.EqualTo(ValidationErrorCodes.Required));
        Assert.That(detail.TryGetProperty("params", out _), Is.False);
    }

    [Test]
    public async Task OverlongString_ProducesTooLongWithTheCanonicalMaxParam()
    {
        var detail = (await PostAsync(new { code = "C1", title = "far too long" }))
            .GetProperty("details").EnumerateArray()
            .Single(d => d.GetProperty("field").GetString() == "title");

        Assert.That(detail.GetProperty("errorCode").GetString(), Is.EqualTo(ValidationErrorCodes.TooLong));
        Assert.That(detail.GetProperty("params").GetProperty("max").GetInt32(), Is.EqualTo(5));
    }

    [Test]
    public async Task MultipleInvalidFields_ProduceOneDetailPerField()
    {
        var fields = (await PostAsync(new { title = "far too long" }))
            .GetProperty("details").EnumerateArray()
            .Select(d => d.GetProperty("field").GetString()).ToArray();

        Assert.That(fields, Is.EquivalentTo(new[] { "code", "title" }));
    }

    [Test]
    public async Task AnyFailure_UsesTheSameTopLevelKeyAndStatus()
    {
        var response = await CreateClient().PostAsJsonAsync("/projects", new { title = "far too long" });
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(envelope.GetProperty("isSuccess").GetBoolean(), Is.False);
        Assert.That(envelope.GetProperty("errorCode").GetString(), Is.EqualTo(ErrorCodes.ValidationFailed));
        Assert.That(envelope.GetProperty("result").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task AnyFailure_UsesCamelCaseJsonFieldPaths()
    {
        var fields = (await PostAsync(new { title = "far too long" }))
            .GetProperty("details").EnumerateArray()
            .Select(d => d.GetProperty("field").GetString()!).ToArray();

        foreach (var field in fields)
        {
            Assert.That(char.IsLower(field[0]), Is.True, $"'{field}' is not camelCase");
        }
    }

    [Test]
    public async Task AnyFailure_PutsNoEnglishSentenceOnTheWire()
    {
        var response = await CreateClient().PostAsJsonAsync("/projects", new { title = "far too long" });
        var body = await response.Content.ReadAsStringAsync();

        foreach (var detail in JsonDocument.Parse(body).RootElement
            .GetProperty("details").EnumerateArray())
        {
            var code = detail.GetProperty("errorCode").GetString()!;
            Assert.That(code, Does.Match("^[A-Z][A-Z0-9_]*$"), $"'{code}' is not a stable key");
        }

        Assert.That(body, Does.Not.Contain("is required"));
        Assert.That(body, Does.Not.Contain("must not be empty"));
        Assert.That(body, Does.Not.Contain("One or more validation errors"));
    }
}
