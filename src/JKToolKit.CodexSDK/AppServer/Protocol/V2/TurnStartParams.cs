using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>turn/start</c> request (v2 protocol).
/// </summary>
public sealed record class TurnStartParams
{
    /// <summary>
    /// Gets the thread identifier to start the turn in.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the input items for the turn (wire payloads).
    /// </summary>
    /// <remarks>
    /// The app-server expects v2 <c>UserInput</c> items. If you are using the higher-level
    /// <see cref="JKToolKit.CodexSDK.AppServer.TurnInputItem"/> API, pass <see cref="JKToolKit.CodexSDK.AppServer.TurnInputItem.Wire"/>
    /// for each item.
    /// </remarks>
    [JsonPropertyName("input")]
    public required IReadOnlyList<object> Input { get; init; }

    /// <summary>
    /// Gets an optional working directory override for this turn and subsequent turns.
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets an optional approval policy override for this turn and subsequent turns (wire value).
    /// </summary>
    /// <remarks>
    /// Known values include <c>untrusted</c>, <c>on-failure</c>, <c>on-request</c>, and <c>never</c>.
    /// </remarks>
    [JsonPropertyName("approvalPolicy")]
    public string? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets an optional sandbox policy override for this turn and subsequent turns.
    /// </summary>
    [JsonPropertyName("sandboxPolicy")]
    public SandboxPolicy.SandboxPolicy? SandboxPolicy { get; init; }

    /// <summary>
    /// Gets an optional model override for this turn and subsequent turns.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Gets an optional reasoning effort override for this turn and subsequent turns (wire value).
    /// </summary>
    /// <remarks>
    /// Known values include <c>none</c>, <c>minimal</c>, <c>low</c>, <c>medium</c>, <c>high</c>, and <c>xhigh</c>.
    /// </remarks>
    [JsonPropertyName("effort")]
    public string? Effort { get; init; }

    /// <summary>
    /// Gets an optional reasoning summary override for this turn and subsequent turns.
    /// </summary>
    /// <remarks>
    /// Known values include <c>auto</c>, <c>concise</c>, <c>detailed</c>, and <c>none</c>.
    /// </remarks>
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    /// <summary>
    /// Gets an optional personality override for this turn and subsequent turns.
    /// </summary>
    [JsonPropertyName("personality")]
    public string? Personality { get; init; }

    /// <summary>
    /// Gets an optional JSON Schema used to constrain the final assistant message.
    /// </summary>
    [JsonPropertyName("outputSchema")]
    public JsonElement? OutputSchema { get; init; }

    /// <summary>
    /// Gets an optional collaboration mode object (experimental).
    /// </summary>
    /// <remarks>
    /// When set, the server may treat this as taking precedence over other overrides such as model, reasoning effort,
    /// and developer instructions.
    /// </remarks>
    [JsonPropertyName("collaborationMode")]
    public JsonElement? CollaborationMode { get; init; }
}
