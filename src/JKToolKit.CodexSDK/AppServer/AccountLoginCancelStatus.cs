namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Status values returned by <c>account/login/cancel</c>.
/// </summary>
public enum AccountLoginCancelStatus
{
    /// <summary>
    /// The server returned an unknown or unsupported status.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The pending login flow was canceled.
    /// </summary>
    Canceled = 1,

    /// <summary>
    /// No pending login flow with the supplied identifier was found.
    /// </summary>
    NotFound = 2
}
