using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

/// <summary>
/// Sandbox policy overrides used by <c>turn/start</c>.
/// </summary>
/// <remarks>
/// This matches the v2 <c>SandboxPolicy</c> DTO offered by <c>codex app-server</c>.
/// </remarks>
public abstract partial record class SandboxPolicy
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

