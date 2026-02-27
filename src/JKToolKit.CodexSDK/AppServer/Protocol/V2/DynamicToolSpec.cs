using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire shape for a dynamic tool specification (v2 protocol).
/// </summary>
/// <remarks>
/// This is used by experimental fields such as <c>thread/start.dynamicTools</c>.
/// </remarks>
public sealed record class DynamicToolSpec
{
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Gets the tool input schema.
    /// </summary>
    /// <remarks>
    /// This should be a JSON Schema object.
    /// </remarks>
    [JsonPropertyName("inputSchema")]
    public required JsonElement InputSchema { get; init; }
}
