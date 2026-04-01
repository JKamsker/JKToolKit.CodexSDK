using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;

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
        NotificationStreamWithRestart(
            streamFactory: (inner, token) => inner.Notifications(token),
            restartMarkerFactory: _options.EmitRestartMarkerNotifications
                ? static e => new ClientRestartedNotification(
                    restartCount: e.RestartCount,
                    previousExitCode: e.PreviousExitCode,
                    timestamp: e.Timestamp,
                    reason: e.Reason,
                    previousStderrTail: e.PreviousStderrTail)
                : null,
            ct);

    public IAsyncEnumerable<AppServerRpcNotification> NotificationsRaw(CancellationToken ct = default) =>
        NotificationStreamWithRestart(
            streamFactory: (inner, token) => inner.NotificationsRaw(token),
            restartMarkerFactory: _options.EmitRestartMarkerNotifications
                ? static e =>
                {
                    var marker = new ClientRestartedNotification(
                        restartCount: e.RestartCount,
                        previousExitCode: e.PreviousExitCode,
                        timestamp: e.Timestamp,
                        reason: e.Reason,
                        previousStderrTail: e.PreviousStderrTail);
                    return new AppServerRpcNotification(marker.Method, marker.Params);
                }
                : null,
            ct);

    private async IAsyncEnumerable<TResult> NotificationStreamWithRestart<TResult>(
        Func<ICodexAppServerClientAdapter, CancellationToken, IAsyncEnumerable<TResult>> streamFactory,
        Func<CodexAppServerRestartEvent, TResult?>? restartMarkerFactory,
        [EnumeratorCancellation] CancellationToken ct)
    {
        _connection.ThrowIfFaultedOrDisposed();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _connection.DisposeToken);
        var linkedToken = linkedCts.Token;

        while (true)
        {
            linkedToken.ThrowIfCancellationRequested();

            var (inner, version) = await _connection.EnsureConnectedWithVersionAsync(linkedToken).ConfigureAwait(false);

            Exception? terminal = null;
            var enumerator = streamFactory(inner, linkedToken).GetAsyncEnumerator(linkedToken);
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

            if (restartMarkerFactory is not null && _connection.LastRestart is not null)
            {
                var marker = restartMarkerFactory(_connection.LastRestart);
                if (marker is not null)
                {
                    yield return marker;
                }
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
            catch (Exception ex) when (TryHandleRetryableFailure(ex, out var isDisconnect, out var decisionException))
            {
                if (isDisconnect)
                {
                    _connection.RememberDisconnect(ex);

                    if (_options.AutoRestart)
                    {
                        await _connection.EnsureRestartedAsync(version, ex, reason: kind.ToString(), linkedToken).ConfigureAwait(false);
                    }
                }

                _connection.ThrowIfFaultedOrDisposed();

                var ensureRestartedAsync = isDisconnect
                    ? new Func<CancellationToken, Task>(c => _connection.EnsureRestartedAsync(version, ex, reason: "policy-ensure-restarted", c))
                    : static _ => Task.CompletedTask;

                var decision = await _options.RetryPolicy(new CodexAppServerRetryContext
                {
                    OperationKind = kind,
                    Attempt = attempt,
                    Exception = decisionException,
                    CancellationToken = linkedToken,
                    EnsureRestartedAsync = ensureRestartedAsync
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

    private static bool TryHandleRetryableFailure(Exception ex, out bool isDisconnect, out Exception decisionException)
    {
        isDisconnect = ResilientAppServerConnection.IsDisconnect(ex);
        decisionException = ex;

        if (isDisconnect)
        {
            return true;
        }

        if (ex is JsonRpcRemoteException rpc && IsServerOverloaded(rpc))
        {
            decisionException = rpc;
            return true;
        }

        if (ex is CodexAppServerRequestFailedException requestFailed && IsServerOverloaded(requestFailed))
        {
            decisionException = requestFailed;
            return true;
        }

        return false;
    }

    private static bool IsServerOverloaded(JsonRpcRemoteException rpc)
    {
        if (rpc.Error.Code == -32001)
        {
            return true;
        }

        if (MessageIndicatesOverload(rpc.Error.Message))
        {
            return true;
        }

        if (rpc.Error.Data is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } data)
        {
            return ContainsRetryToken(data);
        }

        return false;
    }

    private static bool IsServerOverloaded(CodexAppServerRequestFailedException requestFailed)
    {
        if (MessageIndicatesOverload(requestFailed.ErrorMessage))
        {
            return true;
        }

        if (requestFailed.ErrorData is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } data &&
            ContainsRetryToken(data))
        {
            return true;
        }

        return false;
    }

    private static bool MessageIndicatesOverload(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.ToLowerInvariant();
        return normalized.Contains("server overloaded") ||
               normalized.Contains("overload") ||
               normalized.Contains("backpressure") ||
               normalized.Contains("retry later") ||
               normalized.Contains("retry limit") ||
               normalized.Contains("too many failed attempts");
    }

    private static bool ContainsRetryToken(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (IsRetryToken(property.Name))
                    {
                        return true;
                    }

                    if (ContainsRetryToken(property.Value))
                    {
                        return true;
                    }
                }

                return false;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ContainsRetryToken(item))
                    {
                        return true;
                    }
                }

                return false;

            case JsonValueKind.String:
                return IsRetryToken(element.GetString());

            default:
                return false;
        }
    }

    private static bool IsRetryToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();

        return normalized.Contains("serveroverloaded") ||
               normalized.Contains("responsetoomanyfailedattempts") ||
               normalized.Contains("backpressure");
    }
}

