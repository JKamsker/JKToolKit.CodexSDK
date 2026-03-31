using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

public abstract partial record class SandboxPolicy
{
    /// <summary>
    /// Sandbox policy that disallows writing (read-only).
    /// </summary>
    public sealed record class ReadOnly : SandboxPolicy
    {
        /// <inheritdoc />
        public override string Type => "readOnly";

        /// <summary>
        /// Gets an optional value indicating whether outbound network access is allowed while this policy is active.
        /// </summary>
        /// <remarks>
        /// When omitted, the server default is <c>false</c>.
        /// </remarks>
        [JsonPropertyName("networkAccess")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? NetworkAccess { get; init; }

        /// <summary>
        /// Gets optional read-only access controls (upstream feature).
        /// </summary>
        /// <remarks>
        /// When set, older Codex app-server builds may reject this field as invalid params.
        /// </remarks>
        [JsonPropertyName("access")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReadOnlyAccess? Access { get; init; }
    }
}
