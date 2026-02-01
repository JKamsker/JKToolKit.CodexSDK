using JKToolKit.CodexSDK.Public;
using JKToolKit.CodexSDK.Public.Models;

namespace JKToolKit.CodexSDK.Abstractions;

/// <summary>
/// Defines a handle to a Codex session that provides access to session information,
/// event streams, and lifecycle management.
/// </summary>
/// <remarks>
/// This interface represents both live sessions (with a running Codex process) and
/// attached sessions (reading from existing log files). It provides a unified API
/// for consuming session events and monitoring session status.
/// </remarks>
public interface ICodexSessionHandle : IAsyncDisposable
{
    /// <summary>
    /// Gets the session metadata information.
    /// </summary>
    /// <value>
    /// A <see cref="CodexSessionInfo"/> instance containing the session's unique identifier,
    /// log file path, creation timestamp, and other metadata.
    /// </value>
    CodexSessionInfo Info { get; }

    /// <summary>
    /// Gets the reason the Codex session exited, once known.
    /// </summary>
    /// <value>
    /// <see cref="SessionExitReason.Timeout"/> when the session was terminated due to an idle timeout,
    /// <see cref="SessionExitReason.Success"/> when the Codex process exited on its own,
    /// or <see cref="SessionExitReason.Custom"/> when the user explicitly requested termination via <see cref="ExitAsync(CancellationToken)"/>.
    /// Before the process exits, the value is <see cref="SessionExitReason.Unknown"/>.
    /// </value>
    SessionExitReason ExitReason { get; }

    /// <summary>
    /// Gets a value indicating whether this is a live session with a running Codex process.
    /// </summary>
    /// <value>
    /// <c>true</c> if the session has an associated running process; <c>false</c> if it's
    /// a read-only session attached to an existing log file.
    /// </value>
    /// <remarks>
    /// Live sessions can be terminated and have their process monitored for exit.
    /// Non-live sessions (attached sessions) can only read historical events from the log file.
    /// </remarks>
    bool IsLive { get; }

    /// <summary>
    /// Gets an async stream of events from the session log.
    /// </summary>
    /// <param name="options">
    /// Optional configuration for reading the event stream. When null, defaults are used
    /// (reading from the beginning for new sessions, or from the current position for live sessions).
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests during event streaming.
    /// </param>
    /// <returns>
    /// An async enumerable that yields <see cref="CodexEvent"/> instances as they become available.
    /// For live sessions, this stream continues until the session ends or cancellation is requested.
    /// For attached sessions, the stream ends when all existing events have been read.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the session handle has been disposed.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method can be called multiple times to create independent event streams.
    /// Each call creates a new enumeration that respects the provided <paramref name="options"/>.
    /// Events are parsed from the session's JSONL log file and yielded as they are read.
    /// For live sessions, the stream follows the log file as new events are written.
    /// </remarks>
    IAsyncEnumerable<CodexEvent> GetEventsAsync(
        EventStreamOptions? options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Waits for the associated Codex process to exit.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests while waiting.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous wait operation. The task result contains
    /// the exit code of the Codex process.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called on a non-live session (when <see cref="IsLive"/> is <c>false</c>).
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the session handle has been disposed.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <remarks>
    /// This method is only valid for live sessions with an associated process.
    /// For attached sessions, it throws an <see cref="InvalidOperationException"/>.
    /// The method completes when the process exits naturally or is terminated.
    /// Canceling via the <paramref name="cancellationToken"/> does not terminate the process,
    /// it only cancels the wait operation.
    /// </remarks>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to gracefully terminate the associated Codex process and waits for it to exit.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the termination/wait operation.</param>
    /// <returns>
    /// A task whose result is the process exit code. Returns <c>-1</c> if a forceful kill was required.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when called on a non-live session.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the session handle has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    Task<int> ExitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Registers a callback that is invoked when the associated process exits.
    /// </summary>
    /// <param name="callback">The callback to invoke with the process exit code.</param>
    /// <returns>
    /// An <see cref="IDisposable"/> that can be used to unsubscribe the callback. If the process
    /// already exited at registration time, the callback is invoked immediately and the returned
    /// disposable is a no-op.
    /// </returns>
    /// <remarks>
    /// This method is only valid for live sessions; registering on a non-live session will throw.
    /// Callbacks are invoked on a thread-pool thread, and exceptions thrown by callbacks are swallowed
    /// and logged by the implementation.
    /// </remarks>
    IDisposable OnExit(Action<int> callback);
}
