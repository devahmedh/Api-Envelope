using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace TheSpectre.ApiEnvelope;

/// <summary>
/// Writes the response envelope to UTF-8 JSON. This type <b>is</b> the wire contract.
/// </summary>
/// <remarks>
/// The envelope's own properties are written directly, so no reflection is involved and the
/// host application's naming policy, ignore condition and property ordering cannot affect
/// them. Only <c>data</c> is delegated to the caller's <see cref="JsonSerializerOptions"/>,
/// resolved through <see cref="JsonSerializerOptions.TryGetTypeInfo(Type, out System.Text.Json.Serialization.Metadata.JsonTypeInfo)"/>.
/// Under Native AOT that resolves the consumer's source-generated contract for the payload
/// type they are already returning, so no
/// <c>[JsonSerializable(typeof(ApiResponse&lt;T&gt;))]</c> declaration is ever required.
/// </remarks>
public static class ApiEnvelopeWriter
{
    /// <summary>Writes <paramref name="response"/> to <paramref name="writer"/>.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="writer">The destination writer.</param>
    /// <param name="response">The envelope to write.</param>
    /// <param name="options">The serializer options used for the payload only.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// No JSON contract is registered for the payload's runtime type.
    /// </exception>
    public static void Write<T>(
        Utf8JsonWriter writer,
        ApiResponse<T> response,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(options);

        writer.WriteStartObject();

        writer.WriteBoolean("isSuccess", response.IsSuccess);
        writer.WriteNumber("statusCode", response.StatusCode);

        writer.WritePropertyName("data");
        WriteData(writer, response.Data, options);

        if (response.ErrorCode is null)
        {
            writer.WriteNull("errorCode");
        }
        else
        {
            writer.WriteString("errorCode", response.ErrorCode);
        }

        if (response.Message is not null)
        {
            writer.WriteString("message", response.Message);
        }

        writer.WriteString("correlationId", response.CorrelationId);

        if (response.Details is not null)
        {
            WriteDetails(writer, response.Details);
        }

        writer.WriteEndObject();
    }

    /// <summary>Writes <paramref name="response"/> to a new UTF-8 byte array.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="response">The envelope to write.</param>
    /// <param name="options">The serializer options used for the payload only.</param>
    public static byte[] WriteToUtf8Bytes<T>(
        ApiResponse<T> response,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var buffer = new ArrayBufferWriter<byte>();

        // Honour the caller's encoder so the envelope and its payload escape identically.
        // Without this, an app that configured a relaxed encoder would get relaxed escaping
        // in `data` but default escaping in `message` — two rules in one document.
        var writerOptions = new JsonWriterOptions { Encoder = options.Encoder };

        using (var writer = new Utf8JsonWriter(buffer, writerOptions))
        {
            Write(writer, response, options);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteData<T>(Utf8JsonWriter writer, T? data, JsonSerializerOptions options)
    {
        if (data is null)
        {
            writer.WriteNullValue();
            return;
        }

        var runtimeType = data.GetType();

        EnsureResolverConfigured(options);

        if (!options.TryGetTypeInfo(runtimeType, out var typeInfo))
        {
            throw new InvalidOperationException(
                $"No JSON contract is registered for payload type '{runtimeType.FullName}'. " +
                "Under Native AOT, add [JsonSerializable(typeof(" + runtimeType.Name + "))] " +
                "to the JsonSerializerContext registered with ConfigureHttpJsonOptions.");
        }

        JsonSerializer.Serialize(writer, data, typeInfo);
    }

    /// <summary>
    /// Ensures <paramref name="options"/> has a type-info resolver before it is asked for
    /// metadata.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonSerializerOptions.TryGetTypeInfo(Type, out System.Text.Json.Serialization.Metadata.JsonTypeInfo)"/>
    /// only consults a resolver that is already configured — unlike the
    /// <see cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions?)"/> family, it
    /// does not lazily add the reflection-based fallback the first time the options are used.
    /// A host that configured a resolver — including a consumer's source-generated
    /// <c>JsonSerializerContext</c> under Native AOT — is untouched by this call. A host that
    /// configured nothing (the common case for a bare <c>new JsonSerializerOptions(...)</c> in
    /// non-AOT code) gets the reflection resolver populated, matching what
    /// <c>JsonSerializer.Serialize(value, options)</c> would have done for them anyway.
    /// <para>
    /// <see cref="JsonSerializerOptions.MakeReadOnly(bool)"/> itself requires runtime code
    /// generation, so the call is gated behind both
    /// <see cref="RuntimeFeature.IsDynamicCodeSupported"/> and
    /// <see cref="JsonSerializer.IsReflectionEnabledByDefault"/>. The first governs dynamic
    /// code generation and is a compile-time constant <see langword="false"/> under Native AOT,
    /// so the trimmer proves the guarded branch unreachable there and removes it entirely — the
    /// reflection-populating call never ships in an AOT binary. The second governs whether
    /// reflection-based serialization is actually legitimate in the host; it can be
    /// <see langword="false"/> under <c>PublishTrimmed</c> even when dynamic code remains
    /// supported (trimming without AOT), and in that case the reflection resolver must not be
    /// installed, because the trimmer may already have removed the members of the consumer's
    /// DTO that reflection would need — installing it anyway would silently serialize an
    /// incomplete <c>data</c> object instead of failing loudly. A host in that state — trimmed,
    /// not AOT-published, reflection disabled — that has not configured a
    /// <c>TypeInfoResolver</c> for its DTOs must do so explicitly; this method will not paper
    /// over that with reflection, and <see cref="WriteData{T}"/>'s
    /// <see cref="JsonSerializerOptions.TryGetTypeInfo"/> check below fails fast instead.
    /// </para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Only reached when both RuntimeFeature.IsDynamicCodeSupported and " +
            "JsonSerializer.IsReflectionEnabledByDefault are true. The residual risk this " +
            "does not eliminate: a PublishTrimmed (non-AOT) host with reflection enabled but " +
            "whose DTO members were already trimmed away will get an incomplete reflection " +
            "resolver rather than a build-time error. Such a host must configure an explicit " +
            "TypeInfoResolver for its DTOs; this call cannot detect that case.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Guarded by RuntimeFeature.IsDynamicCodeSupported; the call only " +
            "executes when dynamic code, and therefore reflection-based JSON, is supported.")]
    private static void EnsureResolverConfigured(JsonSerializerOptions options)
    {
        if (RuntimeFeature.IsDynamicCodeSupported && JsonSerializer.IsReflectionEnabledByDefault)
        {
            options.MakeReadOnly(populateMissingResolver: true);
        }
    }

    private static void WriteDetails(Utf8JsonWriter writer, IReadOnlyList<ErrorDetail> details)
    {
        writer.WriteStartArray("details");

        for (var i = 0; i < details.Count; i++)
        {
            var detail = details[i];

            writer.WriteStartObject();
            writer.WriteString("field", detail.Field);
            writer.WriteString("errorCode", detail.ErrorCode);

            if (detail.Params is not null)
            {
                writer.WriteStartObject("params");

                foreach (var pair in detail.Params)
                {
                    writer.WritePropertyName(pair.Key);
                    WriteParamValue(writer, pair.Key, pair.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteParamValue(Utf8JsonWriter writer, string key, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            default:
                throw new NotSupportedException(
                    $"Parameter '{key}' has unsupported type '{value.GetType().Name}'. " +
                    "Supported params value types are: string, int, long, decimal, double, bool, null.");
        }
    }
}
