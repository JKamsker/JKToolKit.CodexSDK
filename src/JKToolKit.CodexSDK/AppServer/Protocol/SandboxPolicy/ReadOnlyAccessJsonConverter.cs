using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

/// <summary>
/// JSON converter that serializes <see cref="ReadOnlyAccess"/> polymorphically using the upstream <c>type</c> discriminator.
/// </summary>
public sealed class ReadOnlyAccessJsonConverter : JsonConverter<ReadOnlyAccess>
{
    /// <summary>
    /// Parses <c>readableRoots</c> while ignoring non-string entries for forward compatibility.
    /// </summary>
    private static string[] ParseReadableRoots(JsonElement roots) =>
        roots.ValueKind == JsonValueKind.Array
            ? roots.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty)
                .ToArray()
            : Array.Empty<string>();

    /// <inheritdoc />
    public override ReadOnlyAccess Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("ReadOnlyAccess must be a JSON object.");
        }

        if (!root.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("ReadOnlyAccess must include a string 'type' discriminator.");
        }

        var type = typeProp.GetString();
        return type switch
        {
            "fullAccess" => new ReadOnlyAccess.FullAccess(),
            "restricted" => new ReadOnlyAccess.Restricted
            {
                IncludePlatformDefaults = root.TryGetProperty("includePlatformDefaults", out var include) && include.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? include.GetBoolean()
                    : true,
                ReadableRoots = root.TryGetProperty("readableRoots", out var roots) && roots.ValueKind == JsonValueKind.Array
                    ? ParseReadableRoots(roots)
                    : Array.Empty<string>()
            },
            _ => throw new JsonException($"Unknown ReadOnlyAccess discriminator: '{type}'.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ReadOnlyAccess value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WriteString("type", value.Type);

        switch (value)
        {
            case ReadOnlyAccess.FullAccess:
                break;

            case ReadOnlyAccess.Restricted r:
                writer.WriteBoolean("includePlatformDefaults", r.IncludePlatformDefaults);
                writer.WritePropertyName("readableRoots");
                JsonSerializer.Serialize(writer, r.ReadableRoots ?? Array.Empty<string>(), options);
                break;

            default:
                throw new JsonException($"Unknown {nameof(ReadOnlyAccess)} variant: {value.GetType().Name}");
        }

        writer.WriteEndObject();
    }
}
