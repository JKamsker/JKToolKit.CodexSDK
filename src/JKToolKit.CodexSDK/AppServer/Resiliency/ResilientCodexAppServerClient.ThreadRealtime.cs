using JKToolKit.CodexSDK.AppServer.Protocol.V2;

#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task StartThreadRealtimeAsync(string threadId, string prompt, string? sessionId = null, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadRealtime, (c, token) => c.StartThreadRealtimeAsync(threadId, prompt, sessionId, token), ct);

    public Task AppendThreadRealtimeAudioAsync(string threadId, ThreadRealtimeAudioChunk audio, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadRealtime, (c, token) => c.AppendThreadRealtimeAudioAsync(threadId, audio, token), ct);

    public Task AppendThreadRealtimeTextAsync(string threadId, string text, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadRealtime, (c, token) => c.AppendThreadRealtimeTextAsync(threadId, text, token), ct);

    public Task StopThreadRealtimeAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadRealtime, (c, token) => c.StopThreadRealtimeAsync(threadId, token), ct);
}

#pragma warning restore CS1591
