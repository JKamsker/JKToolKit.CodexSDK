using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/name/set</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadSetNameParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the new thread name, or <see langword="null"/> to clear.
    /// </summary>
    [JsonPropertyName("threadName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? ThreadName { get; init; }
}
