using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>item/fileChange/requestApproval</c> server request (v2 protocol).
/// </summary>
public sealed record class FileChangeRequestApprovalParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.TurnId)]
    public required string TurnId { get; init; }

    /// <summary>
    /// Gets the item identifier that requested approval.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ItemId)]
    public required string ItemId { get; init; }

    /// <summary>
    /// Gets the optional explanatory reason for the file-change approval request.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Reason)]
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the optional root path the agent wants to grant for the rest of the session.
    /// </summary>
    [JsonPropertyName("grantRoot")]
    public string? GrantRoot { get; init; }

    /// <summary>
    /// Gets the ordered list of available decisions as raw JSON union values.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.AvailableDecisions)]
    public List<JsonElement>? AvailableDecisions { get; init; }
}
