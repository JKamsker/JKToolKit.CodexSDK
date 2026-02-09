using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents a start/end byte range in the app-server wire format.
/// </summary>
public sealed record class ByteRange
{
    /// <summary>
    /// Gets the start offset (inclusive).
    /// </summary>
    [JsonPropertyName("start")]
    public uint Start { get; init; }

    /// <summary>
    /// Gets the end offset (exclusive).
    /// </summary>
    [JsonPropertyName("end")]
    public uint End { get; init; }
}
