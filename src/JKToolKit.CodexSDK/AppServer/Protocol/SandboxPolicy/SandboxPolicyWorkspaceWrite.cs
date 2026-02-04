using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public abstract partial record class SandboxPolicy
{
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

