using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

/// <summary>
/// Read-only access controls for sandbox policies (upstream app-server feature).
/// </summary>
/// <remarks>
/// This is an upstream addition and may not be supported by older Codex app-server builds.
/// </remarks>
[JsonConverter(typeof(ReadOnlyAccessJsonConverter))]
public abstract record class ReadOnlyAccess
{
    /// <summary>
    /// Gets the wire discriminator for the read-only access type.
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }

    /// <summary>
    /// Read-only access allowing full read access.
    /// </summary>
    public sealed record class FullAccess : ReadOnlyAccess
    {
        /// <inheritdoc />
        public override string Type => "fullAccess";
    }

    /// <summary>
    /// Read-only access restricting which roots are readable.
    /// </summary>
    public sealed record class Restricted : ReadOnlyAccess
    {
        /// <inheritdoc />
        public override string Type => "restricted";

        /// <summary>
        /// Gets a value indicating whether to include platform default readable roots.
        /// </summary>
        [JsonPropertyName("includePlatformDefaults")]
        public bool IncludePlatformDefaults { get; init; } = true;

        /// <summary>
        /// Gets the explicitly readable roots.
        /// </summary>
        [JsonPropertyName("readableRoots")]
        public IReadOnlyList<string> ReadableRoots { get; init; } = Array.Empty<string>();
    }
}
