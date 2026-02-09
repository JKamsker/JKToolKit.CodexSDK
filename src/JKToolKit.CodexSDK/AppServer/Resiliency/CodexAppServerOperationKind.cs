namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Identifies the kind of operation being performed against the app-server connection.
/// </summary>
public enum CodexAppServerOperationKind
{
    /// <summary>
    /// A user-initiated JSON-RPC request via <c>CallAsync</c>.
    /// </summary>
    Call = 0,

    /// <summary>
    /// A user-initiated <c>thread/start</c> request.
    /// </summary>
    StartThread = 1,

    /// <summary>
    /// A user-initiated <c>thread/resume</c> request.
    /// </summary>
    ResumeThread = 2,

    /// <summary>
    /// A user-initiated <c>turn/start</c> request.
    /// </summary>
    StartTurn = 3,

    /// <summary>
    /// The internal notifications pump that bridges <c>Notifications()</c> across restarts.
    /// </summary>
    NotificationsPump = 4
}

