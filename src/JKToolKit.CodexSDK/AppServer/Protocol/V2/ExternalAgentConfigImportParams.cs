using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>externalAgentConfig/import</c> request (v2 protocol).
/// </summary>
public sealed record class ExternalAgentConfigImportParams
{
    /// <summary>
    /// Gets the migration items to import.
    /// </summary>
    [JsonPropertyName("migrationItems")]
    public required IReadOnlyList<ExternalAgentConfigMigrationItem> MigrationItems { get; init; }
}

