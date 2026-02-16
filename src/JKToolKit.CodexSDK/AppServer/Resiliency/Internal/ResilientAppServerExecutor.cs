using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.AppServer.Resiliency.Internal;

internal sealed class ResilientAppServerExecutor
{
    private readonly ResilientAppServerConnection _connection;
    private readonly CodexAppServerResilienceOptions _options;

    public ResilientAppServerExecutor(ResilientAppServerConnection connection, CodexAppServerResilienceOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct = default) =>
        NotificationsImpl(ct);

    private async IAsyncEnumerable<AppServerNotification> NotificationsImpl([EnumeratorCancellation] CancellationToken ct)
    {
        _connection.ThrowIfFaultedOrDisposed();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _connection.DisposeToken);
        var linkedToken = linkedCts.Token;

        while (true)
        {
            linkedToken.ThrowIfCancellationRequested();

            var (inner, version) = await _connection.EnsureConnectedWithVersionAsync(linkedToken).ConfigureAwait(false);

            Exception? terminal = null;
            var enumerator = inner.Notifications(linkedToken).GetAsyncEnumerator(linkedToken);
            try
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                    catch (Exception ex)
                    {
                        terminal = ex;
                        break;
                    }

                    if (!moved)
                    {
                        terminal = new CodexAppServerDisconnectedException(
                            message: "Codex app-server notifications stream ended unexpectedly.",
                            processId: null,
                            exitCode: null,
                            stderrTail: Array.Empty<string>());
                        break;
                    }

                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            if (terminal is null)
            {
                yield break;
            }

            if (!ResilientAppServerConnection.IsDisconnect(terminal))
            {
                throw terminal;
            }

            _connection.RememberDisconnect(terminal);

            if (!_options.AutoRestart || !_options.NotificationsContinueAcrossRestarts)
            {
                throw terminal;
            }

            await _connection.EnsureRestartedAsync(version, terminal, reason: "notifications-disconnected", linkedToken).ConfigureAwait(false);

            if (_options.EmitRestartMarkerNotifications && _connection.LastRestart is not null)
            {
                var e = _connection.LastRestart;
                yield return new ClientRestartedNotification(
                    restartCount: e.RestartCount,
                    previousExitCode: e.PreviousExitCode,
                    timestamp: e.Timestamp,
                    reason: e.Reason,
                    previousStderrTail: e.PreviousStderrTail);
            }
        }
    }

    public async Task<T> ExecuteWithPolicyAsync<T>(
        CodexAppServerOperationKind kind,
        Func<ICodexAppServerClientAdapter, CancellationToken, Task<T>> op,
        CancellationToken ct)
    {
        _connection.ThrowIfFaultedOrDisposed();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _connection.DisposeToken);
        var linkedToken = linkedCts.Token;

        var attempt = 0;
        while (true)
        {
            linkedToken.ThrowIfCancellationRequested();
            attempt++;

            var (inner, version) = await _connection.EnsureConnectedWithVersionAsync(linkedToken).ConfigureAwait(false);

            try
            {
                return await op(inner, linkedToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ResilientAppServerConnection.IsDisconnect(ex))
            {
                _connection.RememberDisconnect(ex);

                if (_options.AutoRestart)
                {
                    await _connection.EnsureRestartedAsync(version, ex, reason: kind.ToString(), linkedToken).ConfigureAwait(false);
                }

                _connection.ThrowIfFaultedOrDisposed();

                var decision = await _options.RetryPolicy(new CodexAppServerRetryContext
                {
                    OperationKind = kind,
                    Attempt = attempt,
                    Exception = ex,
                    CancellationToken = linkedToken,
                    EnsureRestartedAsync = c => _connection.EnsureRestartedAsync(version, ex, reason: "policy-ensure-restarted", c)
                }).ConfigureAwait(false);

                if (!decision.ShouldRetry)
                {
                    throw;
                }

                if (decision.Delay is { } delay && delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, linkedToken).ConfigureAwait(false);
                }

                if (decision.BeforeRetryAsync is not null)
                {
                    await decision.BeforeRetryAsync(linkedToken).ConfigureAwait(false);
                }
            }
        }
    }
}

