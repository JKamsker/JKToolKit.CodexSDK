using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Starts thread realtime (experimental).
    /// </summary>
    /// <exception cref="CodexExperimentalApiRequiredException">
    /// Thrown when the client was not initialized with <c>experimentalApi</c> capability.
    /// </exception>
    public Task StartThreadRealtimeAsync(string threadId, string prompt, string? sessionId = null, CancellationToken ct = default) =>
        _threadsClient.StartThreadRealtimeAsync(threadId, prompt, sessionId, ct);

    /// <summary>
    /// Appends an audio chunk to thread realtime (experimental).
    /// </summary>
    /// <exception cref="CodexExperimentalApiRequiredException">
    /// Thrown when the client was not initialized with <c>experimentalApi</c> capability.
    /// </exception>
    public Task AppendThreadRealtimeAudioAsync(string threadId, ThreadRealtimeAudioChunk audio, CancellationToken ct = default) =>
        _threadsClient.AppendThreadRealtimeAudioAsync(threadId, audio, ct);

    /// <summary>
    /// Appends input text to thread realtime (experimental).
    /// </summary>
    /// <exception cref="CodexExperimentalApiRequiredException">
    /// Thrown when the client was not initialized with <c>experimentalApi</c> capability.
    /// </exception>
    public Task AppendThreadRealtimeTextAsync(string threadId, string text, CancellationToken ct = default) =>
        _threadsClient.AppendThreadRealtimeTextAsync(threadId, text, ct);

    /// <summary>
    /// Stops thread realtime (experimental).
    /// </summary>
    /// <exception cref="CodexExperimentalApiRequiredException">
    /// Thrown when the client was not initialized with <c>experimentalApi</c> capability.
    /// </exception>
    public Task StopThreadRealtimeAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.StopThreadRealtimeAsync(threadId, ct);
}
