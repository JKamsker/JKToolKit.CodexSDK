using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Controls how the realtime start request should populate the upstream <c>prompt</c> field.
/// </summary>
public enum ThreadRealtimePromptMode
{
    /// <summary>
    /// Omits <c>prompt</c> so upstream uses its default realtime backend prompt.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Sends the supplied prompt verbatim, including empty or whitespace-only strings.
    /// </summary>
    Custom = 1,

    /// <summary>
    /// Sends <c>prompt: null</c> so upstream starts without its default realtime backend prompt.
    /// </summary>
    None = 2
}

/// <summary>
/// Transport settings for <c>thread/realtime/start</c>.
/// </summary>
public sealed class ThreadRealtimeTransport
{
    /// <summary>
    /// Gets the transport kind wire value.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the SDP offer when using WebRTC transport.
    /// </summary>
    public string? Sdp { get; }

    private ThreadRealtimeTransport(string type, string? sdp)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Transport type cannot be empty or whitespace.", nameof(type));
        }

        if (string.Equals(type, "webrtc", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(sdp))
        {
            throw new ArgumentException("WebRTC transport requires a non-empty SDP offer.", nameof(sdp));
        }

        Type = type;
        Sdp = sdp;
    }

    /// <summary>
    /// Gets the websocket realtime transport.
    /// </summary>
    public static ThreadRealtimeTransport WebSocket { get; } = new("websocket", sdp: null);

    /// <summary>
    /// Creates a WebRTC realtime transport from a browser-generated SDP offer.
    /// </summary>
    public static ThreadRealtimeTransport WebRtc(string sdp) => new("webrtc", sdp);

    internal JsonElement ToJson() =>
        string.Equals(Type, "webrtc", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.SerializeToElement(new { type = Type, sdp = Sdp })
            : JsonSerializer.SerializeToElement(new { type = Type });
}

/// <summary>
/// Options for starting thread realtime via the app-server.
/// </summary>
public sealed class ThreadRealtimeStartOptions
{
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets how the upstream realtime prompt should be populated.
    /// </summary>
    public ThreadRealtimePromptMode PromptMode { get; set; }

    /// <summary>
    /// Gets or sets the custom realtime prompt used when <see cref="PromptMode"/> is
    /// <see cref="ThreadRealtimePromptMode.Custom"/>.
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Gets or sets the optional realtime session identifier.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the optional realtime transport override.
    /// </summary>
    public ThreadRealtimeTransport? Transport { get; set; }

    /// <summary>
    /// Gets or sets the optional realtime voice identifier.
    /// </summary>
    /// <remarks>
    /// Known values upstream currently include <c>alloy</c>, <c>ash</c>, <c>marin</c>, <c>sage</c>, and others.
    /// </remarks>
    public string? Voice { get; set; }
}
