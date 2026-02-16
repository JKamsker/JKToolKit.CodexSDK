using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal interface ICodexAppServerClientAdapter : IAsyncDisposable
{
    Task ExitTask { get; }

    Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct);

    IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct);

    Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct);

    Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct);

    Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct);

    Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct);
}

internal sealed class CodexAppServerClientAdapter : ICodexAppServerClientAdapter
{
    private readonly CodexAppServerClient _inner;

    public CodexAppServerClientAdapter(CodexAppServerClient inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task ExitTask => _inner.ExitTask;

    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct) => _inner.CallAsync(method, @params, ct);

    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct) => _inner.Notifications(ct);

    public Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct) => _inner.StartThreadAsync(options, ct);

    public Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct) => _inner.ResumeThreadAsync(threadId, ct);

    public Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct) => _inner.ResumeThreadAsync(options, ct);

    public Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct) => _inner.StartTurnAsync(threadId, options, ct);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

