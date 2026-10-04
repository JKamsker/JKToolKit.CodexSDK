using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

/// <summary>
/// Sandbox policy overrides used by <c>turn/start</c>.
/// </summary>
/// <remarks>
/// This matches the v2 <c>SandboxPolicy</c> DTO offered by <c>codex app-server</c>.
/// </remarks>
[JsonConverter(typeof(SandboxPolicyJsonConverter))]
public abstract partial record class SandboxPolicy
{
    /// <summary>
    /// Gets the wire discriminator for the sandbox policy type.
    /// </summary>
    /// <remarks>
    /// Custom policy variants should annotate their override with <c>[JsonPropertyName("type")]</c>
    /// so the wire name remains stable with all serializer naming policies.
    /// </remarks>
    [JsonPropertyName(JsonFieldNames.Type)]
    public abstract string Type { get; }
}
