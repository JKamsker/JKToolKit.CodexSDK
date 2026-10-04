using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

public abstract partial record class SandboxPolicy
{
    /// <summary>
    /// Sandbox policy that allows full access (no sandbox restrictions).
    /// </summary>
    public sealed record class DangerFullAccess : SandboxPolicy
    {
        /// <inheritdoc />
        [JsonPropertyName(JsonFieldNames.Type)]
        public override string Type => "dangerFullAccess";
    }
}
