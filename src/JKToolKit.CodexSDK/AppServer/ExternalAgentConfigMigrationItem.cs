using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Closed item types supported by <c>externalAgentConfig/detect</c> and <c>externalAgentConfig/import</c>.
/// </summary>
public enum ExternalAgentConfigMigrationItemType
{
    /// <summary>
    /// Migrate an <c>AGENTS.md</c> file.
    /// </summary>
    AgentsMd = 0,

    /// <summary>
    /// Migrate Codex/agent config content.
    /// </summary>
    Config = 1,

    /// <summary>
    /// Migrate skills configuration.
    /// </summary>
    Skills = 2,

    /// <summary>
    /// Migrate MCP server configuration.
    /// </summary>
    McpServerConfig = 3
}

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
    [JsonPropertyName("itemType")]
    [JsonConverter(typeof(ExternalAgentConfigMigrationItemTypeJsonConverter))]
    public required ExternalAgentConfigMigrationItemType ItemType { get; init; }
}

internal static class ExternalAgentConfigMigrationItemTypeExtensions
{
    public static string ToWireValue(this ExternalAgentConfigMigrationItemType itemType) =>
        itemType switch
        {
            ExternalAgentConfigMigrationItemType.AgentsMd => "AGENTS_MD",
            ExternalAgentConfigMigrationItemType.Config => "CONFIG",
            ExternalAgentConfigMigrationItemType.Skills => "SKILLS",
            ExternalAgentConfigMigrationItemType.McpServerConfig => "MCP_SERVER_CONFIG",
            _ => throw new ArgumentOutOfRangeException(nameof(itemType), itemType, "Unknown external agent config migration item type.")
        };

    public static bool TryParseWireValue(string? value, out ExternalAgentConfigMigrationItemType itemType)
    {
        switch (value)
        {
            case "AGENTS_MD":
                itemType = ExternalAgentConfigMigrationItemType.AgentsMd;
                return true;
            case "CONFIG":
                itemType = ExternalAgentConfigMigrationItemType.Config;
                return true;
            case "SKILLS":
                itemType = ExternalAgentConfigMigrationItemType.Skills;
                return true;
            case "MCP_SERVER_CONFIG":
                itemType = ExternalAgentConfigMigrationItemType.McpServerConfig;
                return true;
            default:
                itemType = default;
                return false;
        }
    }
}

internal sealed class ExternalAgentConfigMigrationItemTypeJsonConverter : JsonConverter<ExternalAgentConfigMigrationItemType>
{
    public override ExternalAgentConfigMigrationItemType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (ExternalAgentConfigMigrationItemTypeExtensions.TryParseWireValue(value, out var itemType))
        {
            return itemType;
        }

        throw new JsonException($"Unknown external agent config migration item type '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, ExternalAgentConfigMigrationItemType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToWireValue());
}
