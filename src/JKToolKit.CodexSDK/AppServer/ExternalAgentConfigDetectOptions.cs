using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for detecting external agent configuration that can be migrated into Codex.
/// </summary>
public sealed class ExternalAgentConfigDetectOptions
{
    /// <summary>
    /// Gets or sets zero or more working directories to include for repo-scoped detection.
    /// </summary>
    [JsonPropertyName("cwds")]
    public IReadOnlyList<string>? Cwds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include detection under the user's home directory.
    /// </summary>
    [JsonPropertyName("includeHome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IncludeHome { get; set; }

    /// <summary>
    /// Gets or sets the maximum age in days for detected sessions.
    /// </summary>
    [JsonPropertyName("maxSessionAgeDays")]
    public int? MaxSessionAgeDays { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of sessions to detect.
    /// </summary>
    [JsonPropertyName("maxSessions")]
    public int? MaxSessions { get; set; }

    /// <summary>
    /// Gets or sets the migration-source selector.
    /// </summary>
    [JsonPropertyName("migrationSource")]
    public string? MigrationSource { get; set; }
}
