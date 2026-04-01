using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>mcpServer/elicitation/request</c> server request (v2 protocol).
/// </summary>
public sealed record class McpServerElicitationRequestParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the optional correlated turn identifier.
    /// </summary>
    [JsonPropertyName("turnId")]
    public string? TurnId { get; init; }

    /// <summary>
    /// Gets the MCP server name that initiated the elicitation.
    /// </summary>
    [JsonPropertyName("serverName")]
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets the elicitation mode.
    /// </summary>
    [JsonPropertyName("mode")]
    public required McpServerElicitationMode Mode { get; init; }

    /// <summary>
    /// Gets the user-facing prompt message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Gets the requested schema for form-mode elicitations.
    /// </summary>
    [JsonPropertyName("requestedSchema")]
    public JsonElement? RequestedSchema { get; init; }

    /// <summary>
    /// Gets the URL for URL-mode elicitations.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>
    /// Gets the upstream elicitation identifier for URL-mode requests.
    /// </summary>
    [JsonPropertyName("elicitationId")]
    public string? ElicitationId { get; init; }

    /// <summary>
    /// Gets optional request metadata.
    /// </summary>
    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

/// <summary>
/// Supported elicitation modes.
/// </summary>
[JsonConverter(typeof(McpServerElicitationModeJsonConverter))]
public enum McpServerElicitationMode
{
    /// <summary>
    /// Schema-backed form input.
    /// </summary>
    Form = 0,

    /// <summary>
    /// Browser or URL-based continuation.
    /// </summary>
    Url = 1
}

/// <summary>
/// Supported elicitation response actions.
/// </summary>
[JsonConverter(typeof(McpServerElicitationActionJsonConverter))]
public enum McpServerElicitationAction
{
    /// <summary>
    /// Accept the elicitation.
    /// </summary>
    Accept = 0,

    /// <summary>
    /// Decline the elicitation.
    /// </summary>
    Decline = 1,

    /// <summary>
    /// Cancel the elicitation.
    /// </summary>
    Cancel = 2
}

/// <summary>
/// Wire response payload for the <c>mcpServer/elicitation/request</c> server request (v2 protocol).
/// </summary>
public sealed record class McpServerElicitationRequestResponse
{
    /// <summary>
    /// Gets the client response action.
    /// </summary>
    [JsonPropertyName("action")]
    public required McpServerElicitationAction Action { get; init; }

    /// <summary>
    /// Gets optional structured content for accepted elicitations.
    /// </summary>
    [JsonPropertyName("content")]
    public JsonElement? Content { get; init; }

    /// <summary>
    /// Gets optional client metadata.
    /// </summary>
    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class McpServerElicitationModeJsonConverter : JsonConverter<McpServerElicitationMode>
{
    public override McpServerElicitationMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "form" => McpServerElicitationMode.Form,
            "url" => McpServerElicitationMode.Url,
            var value => throw new JsonException($"Unknown MCP elicitation mode '{value}'.")
        };

    public override void Write(Utf8JsonWriter writer, McpServerElicitationMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            McpServerElicitationMode.Form => "form",
            McpServerElicitationMode.Url => "url",
            _ => throw new JsonException($"Unknown MCP elicitation mode '{value}'.")
        });
    }
}

internal sealed class McpServerElicitationActionJsonConverter : JsonConverter<McpServerElicitationAction>
{
    public override McpServerElicitationAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "accept" => McpServerElicitationAction.Accept,
            "decline" => McpServerElicitationAction.Decline,
            "cancel" => McpServerElicitationAction.Cancel,
            var value => throw new JsonException($"Unknown MCP elicitation action '{value}'.")
        };

    public override void Write(Utf8JsonWriter writer, McpServerElicitationAction value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            McpServerElicitationAction.Accept => "accept",
            McpServerElicitationAction.Decline => "decline",
            McpServerElicitationAction.Cancel => "cancel",
            _ => throw new JsonException($"Unknown MCP elicitation action '{value}'.")
        });
    }
}
