using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents standalone tool output submitted through <c>turn/start</c>.
/// </summary>
/// <remarks>
/// Upstream accepts <see cref="Output"/> as either a string or an array of function-call output content items.
/// </remarks>
public sealed class TurnToolOutput
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the optional tool namespace.
    /// </summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the raw output body.
    /// </summary>
    [JsonPropertyName("output")]
    public required JsonElement Output { get; set; }

    /// <summary>
    /// Creates text tool output.
    /// </summary>
    public static TurnToolOutput Text(string name, string output, string? @namespace = null) =>
        new()
        {
            Name = name,
            Namespace = @namespace,
            Output = JsonSerializer.SerializeToElement(output)
        };
}
