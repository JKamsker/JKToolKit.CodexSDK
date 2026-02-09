namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Context passed to a retry policy when an operation fails due to disconnect.
/// </summary>
public sealed record class CodexAppServerRetryContext
{
    /// <summary>
    /// Gets the operation kind.
    /// </summary>
    public required CodexAppServerOperationKind OperationKind { get; init; }

    /// <summary>
    /// Gets the retry attempt number (1-based).
    /// </summary>
    public required int Attempt { get; init; }

    /// <summary>
    /// Gets the exception that triggered the retry decision.
    /// </summary>
    public required Exception Exception { get; init; }

    /// <summary>
    /// Gets the cancellation token for the operation.
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Restarts/reconnects the app-server subprocess if needed.
    /// </summary>
    public required Func<CancellationToken, Task> EnsureRestartedAsync { get; init; }
}

