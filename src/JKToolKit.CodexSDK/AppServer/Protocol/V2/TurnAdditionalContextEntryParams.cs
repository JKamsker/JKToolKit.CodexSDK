using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire entry for turn-scoped additional context.
/// </summary>
public sealed record class TurnAdditionalContextEntryParams
{
    /// <summary>
    /// Gets the context text value.
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    /// <summary>
    /// Gets the context kind. Known values are <c>untrusted</c> and <c>application</c>.
    /// </summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }
}
