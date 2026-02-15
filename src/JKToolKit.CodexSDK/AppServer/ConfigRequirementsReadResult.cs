using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of <c>configRequirements/read</c>.
/// </summary>
public sealed record class ConfigRequirementsReadResult
{
    /// <summary>
    /// Gets the parsed requirements object, or null if no requirements are configured.
    /// </summary>
    public ConfigRequirements? Requirements { get; init; }

    /// <summary>
    /// Gets the raw JSON-RPC result payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

