using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

/// <summary>
/// Sandbox policy overrides used by <c>turn/start</c>.
/// </summary>
/// <remarks>
/// This matches the v2 <c>SandboxPolicy</c> DTO offered by <c>codex app-server</c>.
/// </remarks>
public abstract record class SandboxPolicy
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }

    public sealed record class DangerFullAccess : SandboxPolicy
    {
        public override string Type => "dangerFullAccess";
    }

    public sealed record class ReadOnly : SandboxPolicy
    {
        public override string Type => "readOnly";
    }

    public sealed record class ExternalSandbox : SandboxPolicy
    {
        public override string Type => "externalSandbox";

        [JsonPropertyName("networkAccess")]
        public required string NetworkAccess { get; init; }
    }

    public sealed record class WorkspaceWrite : SandboxPolicy
    {
        public override string Type => "workspaceWrite";

        [JsonPropertyName("writableRoots")]
        public IReadOnlyList<string> WritableRoots { get; init; } = Array.Empty<string>();

        [JsonPropertyName("networkAccess")]
        public bool NetworkAccess { get; init; }

        [JsonPropertyName("excludeTmpdirEnvVar")]
        public bool ExcludeTmpdirEnvVar { get; init; }

        [JsonPropertyName("excludeSlashTmp")]
        public bool ExcludeSlashTmp { get; init; }
    }
}
