using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter : IAsyncDisposable
{
    Task ExitTask { get; }
    AppServerInitializeResult? InitializeResult { get; }
    AppServerNotificationDropStats NotificationDropStats { get; }

    Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct);

    Task<TResult?> CallAsync<TResult>(string method, object? @params, JsonSerializerOptions? serializerOptions, CancellationToken ct);

    IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct);
    IAsyncEnumerable<AppServerRpcNotification> NotificationsRaw(CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter : ICodexAppServerClientAdapter
{
    private readonly CodexAppServerClient _inner;

    public CodexAppServerClientAdapter(CodexAppServerClient inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task ExitTask => _inner.ExitTask;
    public AppServerInitializeResult? InitializeResult => _inner.InitializeResult;
    public AppServerNotificationDropStats NotificationDropStats => _inner.NotificationDropStats;

    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct) => _inner.CallAsync(method, @params, ct);

    public Task<TResult?> CallAsync<TResult>(string method, object? @params, JsonSerializerOptions? serializerOptions, CancellationToken ct) =>
        _inner.CallAsync<TResult>(method, @params, serializerOptions, ct);

    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct) => _inner.Notifications(ct);
    public IAsyncEnumerable<AppServerRpcNotification> NotificationsRaw(CancellationToken ct) => _inner.NotificationsRaw(ct);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

