using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using JKToolKit.CodexSDK.Infrastructure.Internal;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerClientCore
{
    private async Task WatchProcessExitAsync()
    {
        try
        {
            await _process.Completion.ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            SignalDisconnect(BuildDisconnectException());
        }
        catch
        {
            SignalDisconnect(new CodexAppServerDisconnectedException(
                "Codex app-server subprocess disconnected.",
                processId: null,
                exitCode: null,
                stderrTail: Array.Empty<string>()));
        }
    }

    private Exception BuildDisconnectException()
    {
        var stderrTailRaw = Array.Empty<string>();
        try
        {
            stderrTailRaw = _process.StderrTail.ToArray();
        }
        catch
        {
            // ignore
        }

        var stderrTail = CodexDiagnosticsSanitizer.SanitizeLines(stderrTailRaw, maxLines: 20, maxCharsPerLine: 400);

        int? exitCode = null;
        int? pid = null;
        try { exitCode = _process.ExitCode; } catch { /* ignore */ }
        try { pid = _process.ProcessId; } catch { /* ignore */ }

        var msg = exitCode is null
            ? "Codex app-server subprocess disconnected."
            : $"Codex app-server subprocess exited with code {exitCode}.";

        // Note: include only redacted/truncated stderr snippets to reduce accidental leakage when exception messages are logged.
        if (stderrTail.Length > 0)
            msg += $" (stderr tail, redacted: {string.Join(" | ", stderrTail.TakeLast(5))})";

        return new CodexAppServerDisconnectedException(
            msg,
            processId: pid,
            exitCode: exitCode,
            stderrTail: stderrTail);
    }

    private void SignalDisconnect(Exception ex)
    {
        if (Interlocked.Exchange(ref _disconnectSignaled, 1) != 0)
        {
            return;
        }

        try
        {
            _globalNotifications.Writer.TryComplete(ex);
            _globalRawNotifications.Writer.TryComplete(ex);

            var handles = SnapshotAndClearTurns();

            foreach (var handle in handles)
            {
                handle.EventsChannel.Writer.TryComplete(ex);
                handle.RawEventsChannel.Writer.TryComplete(ex);
                handle.CompletionTcs.TrySetException(ex);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static string? TryGetTurnId(AppServerNotification notification)
    {
        var turnId = notification switch
        {
            AgentMessageDeltaNotification d => d.TurnId,
            ItemStartedNotification s => s.TurnId,
            ItemCompletedNotification c => c.TurnId,
            TurnStartedNotification s => s.TurnId,
            TurnDiffUpdatedNotification d => d.TurnId,
            TurnPlanUpdatedNotification p => p.TurnId,
            ThreadTokenUsageUpdatedNotification u => u.TurnId,
            PlanDeltaNotification d => d.TurnId,
            RawResponseItemCompletedNotification r => r.TurnId,
            CommandExecutionOutputDeltaNotification d => d.TurnId,
            TerminalInteractionNotification t => t.TurnId,
            FileChangeOutputDeltaNotification d => d.TurnId,
            McpToolCallProgressNotification p => p.TurnId,
            ReasoningSummaryTextDeltaNotification d => d.TurnId,
            ReasoningSummaryPartAddedNotification d => d.TurnId,
            ReasoningTextDeltaNotification d => d.TurnId,
            ContextCompactedNotification c => c.TurnId,
            ErrorNotification e => e.TurnId,
            TurnCompletedNotification t => t.TurnId,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(turnId))
        {
            return turnId;
        }

        return TryGetTurnIdFromParams(notification.Params);
    }

    private static string? TryGetTurnIdFromParams(JsonElement @params)
    {
        if (@params.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (@params.TryGetProperty("turnId", out var turnIdProp) &&
            turnIdProp.ValueKind == JsonValueKind.String)
        {
            var value = turnIdProp.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (@params.TryGetProperty("turn", out var turnProp))
        {
            if (turnProp.ValueKind == JsonValueKind.String)
            {
                var value = turnProp.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            if (turnProp.ValueKind == JsonValueKind.Object &&
                turnProp.TryGetProperty("id", out var turnIdNestedProp) &&
                turnIdNestedProp.ValueKind == JsonValueKind.String)
            {
                var value = turnIdNestedProp.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
