using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

/// <summary>
/// Sandbox policy overrides used by <c>turn/start</c>.
/// </summary>
/// <remarks>
/// This matches the v2 <c>SandboxPolicy</c> DTO offered by <c>codex app-server</c>.
/// </remarks>
public abstract record SandboxPolicy
{
    private SandboxPolicy()
    {
    }

    public sealed record DangerFullAccess : SandboxPolicy
    {
        [JsonPropertyName("type")]
        public string Type => "dangerFullAccess";
    }

    public sealed record ReadOnly : SandboxPolicy
    {
        [JsonPropertyName("type")]
        public string Type => "readOnly";
    }

    public sealed record ExternalSandbox(
        [property: JsonPropertyName("networkAccess")] string NetworkAccess)
        : SandboxPolicy
    {
        [JsonPropertyName("type")]
        public string Type => "externalSandbox";
    }

    public sealed record WorkspaceWrite(
        [property: JsonPropertyName("writableRoots")] IReadOnlyList<string> WritableRoots,
        [property: JsonPropertyName("networkAccess")] bool NetworkAccess,
        [property: JsonPropertyName("excludeTmpdirEnvVar")] bool ExcludeTmpdirEnvVar,
        [property: JsonPropertyName("excludeSlashTmp")] bool ExcludeSlashTmp)
        : SandboxPolicy
    {
        [JsonPropertyName("type")]
        public string Type => "workspaceWrite";
    }
}

