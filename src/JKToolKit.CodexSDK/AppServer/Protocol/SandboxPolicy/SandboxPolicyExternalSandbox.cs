using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public abstract partial record class SandboxPolicy
{
    public sealed record class ExternalSandbox : SandboxPolicy
    {
        public override string Type => "externalSandbox";

        [JsonPropertyName("networkAccess")]
        public required string NetworkAccess { get; init; }
    }
}

