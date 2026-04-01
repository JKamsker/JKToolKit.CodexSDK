using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task StartThreadRealtimeAsync(string threadId, string prompt, string? sessionId, CancellationToken ct);

    Task AppendThreadRealtimeAudioAsync(string threadId, ThreadRealtimeAudioChunk audio, CancellationToken ct);

    Task AppendThreadRealtimeTextAsync(string threadId, string text, CancellationToken ct);

    Task StopThreadRealtimeAsync(string threadId, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task StartThreadRealtimeAsync(string threadId, string prompt, string? sessionId, CancellationToken ct) => _inner.StartThreadRealtimeAsync(threadId, prompt, sessionId, ct);

    public Task AppendThreadRealtimeAudioAsync(string threadId, ThreadRealtimeAudioChunk audio, CancellationToken ct) => _inner.AppendThreadRealtimeAudioAsync(threadId, audio, ct);

    public Task AppendThreadRealtimeTextAsync(string threadId, string text, CancellationToken ct) => _inner.AppendThreadRealtimeTextAsync(threadId, text, ct);

    public Task StopThreadRealtimeAsync(string threadId, CancellationToken ct) => _inner.StopThreadRealtimeAsync(threadId, ct);
}
