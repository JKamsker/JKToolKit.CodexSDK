using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents a start/end byte range in the app-server wire format.
/// </summary>
public sealed record class ByteRange
{
    /// <summary>
    /// Gets the start offset (inclusive).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Start)]
    public uint Start { get; init; }

    /// <summary>
    /// Gets the end offset (exclusive).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.End)]
    public uint End { get; init; }
}
