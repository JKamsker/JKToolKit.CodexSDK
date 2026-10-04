using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

internal sealed class SandboxPolicyJsonConverter : JsonConverter<SandboxPolicy>
{
    public override SandboxPolicy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("SandboxPolicy must be a JSON object.");
        }

        if (!root.TryGetProperty(JsonFieldNames.Type, out var type) || type.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("SandboxPolicy must include a string 'type' discriminator.");
        }

        return type.GetString() switch
        {
            "dangerFullAccess" => root.Deserialize<SandboxPolicy.DangerFullAccess>(options)!,
            "externalSandbox" => root.Deserialize<SandboxPolicy.ExternalSandbox>(options)!,
            "readOnly" => root.Deserialize<SandboxPolicy.ReadOnly>(options)!,
            "workspaceWrite" => root.Deserialize<SandboxPolicy.WorkspaceWrite>(options)!,
            var unknown => throw new JsonException($"Unknown SandboxPolicy discriminator: '{unknown}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, SandboxPolicy value, JsonSerializerOptions options)
    {
        if (value is not (SandboxPolicy.DangerFullAccess or SandboxPolicy.ExternalSandbox or
            SandboxPolicy.ReadOnly or SandboxPolicy.WorkspaceWrite))
        {
            throw new JsonException($"Unknown SandboxPolicy variant: {value.GetType().Name}");
        }

        // The runtime contract carries the variant fields; the base contract only carries Type.
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
