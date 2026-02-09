using System.Text.Json.Serialization;

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
        /// Gets the outbound network access state (<c>restricted</c> or <c>enabled</c>).
        /// </summary>
        [JsonPropertyName("networkAccess")]
        public required string NetworkAccess { get; init; }
    }
}
