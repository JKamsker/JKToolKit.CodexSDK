using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a thread as returned by the Codex app server.
/// </summary>
public sealed record class CodexThread
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the raw JSON payload for the thread.
    /// </summary>
    public JsonElement Raw { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CodexThread"/>.
    /// </summary>
    public CodexThread(string id, JsonElement raw)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Raw = raw;
    }
}

