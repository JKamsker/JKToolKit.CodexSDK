using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

public abstract partial record class SandboxPolicy
{
    /// <summary>
    /// Sandbox policy used when the client enforces sandboxing externally.
    /// </summary>
    /// <remarks>
    /// This policy indicates the process is already running inside an external sandbox. Codex treats disk access as unrestricted
    /// while honoring the declared outbound network access state.
    /// </remarks>
    public sealed record class ExternalSandbox : SandboxPolicy
    {
        /// <inheritdoc />
        public override string Type => "externalSandbox";

        /// <summary>
        /// Gets the optional outbound network access override (<c>restricted</c> or <c>enabled</c>).
        /// </summary>
        /// <remarks>
        /// When omitted, the server default is <c>restricted</c>.
        /// </remarks>
        [JsonPropertyName(JsonFieldNames.NetworkAccess)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(SandboxNetworkAccessJsonConverter))]
        public SandboxNetworkAccess? NetworkAccess { get; init; }
    }
}
