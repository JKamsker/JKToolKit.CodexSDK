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
}
