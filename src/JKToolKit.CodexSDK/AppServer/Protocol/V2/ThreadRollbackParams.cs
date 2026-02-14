using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/rollback</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadRollbackParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the number of turns to roll back.
    /// </summary>
    [JsonPropertyName("numTurns")]
    public int NumTurns { get; init; }
}

