using System.Text.Json.Serialization;
using System.IO;

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
        private IReadOnlyList<string> _readableRoots = Array.Empty<string>();

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
        public IReadOnlyList<string> ReadableRoots
        {
            get => _readableRoots;
            init => _readableRoots = ValidateAbsolutePaths(value, nameof(ReadableRoots));
        }

        private static IReadOnlyList<string> ValidateAbsolutePaths(IReadOnlyList<string>? paths, string parameterName)
        {
            if (paths is null)
            {
                return Array.Empty<string>();
            }

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("Readable roots cannot contain null, empty, or whitespace paths.", parameterName);

                if (!Path.IsPathFullyQualified(path))
                    throw new ArgumentException($"Readable root '{path}' must be an absolute path.", parameterName);
            }

            return paths;
        }
    }
}
