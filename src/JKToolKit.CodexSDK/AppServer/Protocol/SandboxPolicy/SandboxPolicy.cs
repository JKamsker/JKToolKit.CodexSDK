using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

/// <summary>
/// Sandbox policy overrides used by <c>turn/start</c>.
/// </summary>
/// <remarks>
/// This matches the v2 <c>SandboxPolicy</c> DTO offered by <c>codex app-server</c>.
/// </remarks>
public abstract partial record class SandboxPolicy
{
    /// <summary>
    /// Gets the wire discriminator for the sandbox policy type.
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}
