using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the response payload from <c>externalAgentConfig/detect</c>.
/// </summary>
public sealed record class ExternalAgentConfigDetectResult
{
    /// <summary>
    /// Gets the detected migration items.
    /// </summary>
    public required IReadOnlyList<ExternalAgentConfigMigrationItem> Items { get; init; }

    /// <summary>
    /// Gets the raw JSON response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
