using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.Initialize;

/// <summary>
/// Client-declared capabilities negotiated during initialize.
/// </summary>
public sealed record class InitializeCapabilities
{
    /// <summary>
    /// Gets a value indicating whether to opt into experimental API features.
    /// </summary>
    [JsonPropertyName("experimentalApi")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExperimentalApi { get; init; }

    /// <summary>
    /// Gets an optional list of notification method names to opt out of.
    /// </summary>
    [JsonPropertyName("optOutNotificationMethods")]
    public IReadOnlyList<string>? OptOutNotificationMethods { get; init; }
}
