namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Status values returned by <c>account/login/cancel</c>.
/// </summary>
public enum AccountLoginCancelStatus
{
    /// <summary>
    /// The pending login flow was canceled.
    /// </summary>
    Canceled = 0,

    /// <summary>
    /// No pending login flow with the supplied identifier was found.
    /// </summary>
    NotFound = 1
}
