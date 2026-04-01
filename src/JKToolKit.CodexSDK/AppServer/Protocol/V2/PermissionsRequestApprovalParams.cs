using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>item/permissions/requestApproval</c> server request (v2 protocol).
/// </summary>
public sealed record class PermissionsRequestApprovalParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    [JsonPropertyName("turnId")]
    public required string TurnId { get; init; }

    /// <summary>
    /// Gets the item identifier that requested additional permissions.
    /// </summary>
    [JsonPropertyName("itemId")]
    public required string ItemId { get; init; }

    /// <summary>
    /// Gets the optional user-facing reason for the request.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the requested permission profile as raw JSON.
    /// </summary>
    [JsonPropertyName("permissions")]
    public required JsonElement Permissions { get; init; }
}

/// <summary>
/// Scope of a granted permission profile returned to the app-server.
/// </summary>
[JsonConverter(typeof(PermissionGrantScopeJsonConverter))]
public enum PermissionGrantScope
{
    /// <summary>
    /// Grant only for the current turn.
    /// </summary>
    Turn = 0,

    /// <summary>
    /// Persist the grant for the current session.
    /// </summary>
    Session = 1
}

/// <summary>
/// Wire response payload for the <c>item/permissions/requestApproval</c> server request (v2 protocol).
/// </summary>
public sealed record class PermissionsRequestApprovalResponse
{
    /// <summary>
    /// Gets the granted subset of the requested permission profile.
    /// </summary>
    [JsonPropertyName("permissions")]
    public required JsonElement Permissions { get; init; }

    /// <summary>
    /// Gets or sets the grant scope. Defaults to the current turn.
    /// </summary>
    [JsonPropertyName("scope")]
    public PermissionGrantScope Scope { get; init; } = PermissionGrantScope.Turn;
}

internal sealed class PermissionGrantScopeJsonConverter : JsonConverter<PermissionGrantScope>
{
    public override PermissionGrantScope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "turn" => PermissionGrantScope.Turn,
            "session" => PermissionGrantScope.Session,
            var value => throw new JsonException($"Unknown permission grant scope '{value}'.")
        };

    public override void Write(Utf8JsonWriter writer, PermissionGrantScope value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            PermissionGrantScope.Turn => "turn",
            PermissionGrantScope.Session => "session",
            _ => throw new JsonException($"Unknown permission grant scope '{value}'.")
        });
    }
}
