using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a single external agent config migration item.
/// </summary>
public sealed record class ExternalAgentConfigMigrationItem
{
    /// <summary>
    /// Gets the working directory this item is associated with.
    /// </summary>
    /// <remarks>
    /// Null or empty means home-scoped migration; non-empty means repo-scoped migration.
    /// </remarks>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets a human-readable description of what will be migrated.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Gets the migration item type identifier.
    /// </summary>
    /// <remarks>
    /// Known values include <c>AGENTS_MD</c>, <c>CONFIG</c>, <c>SKILLS</c>, and <c>MCP_SERVER_CONFIG</c>.
    /// </remarks>
    [JsonPropertyName("itemType")]
    public required string ItemType { get; init; }
}
