namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Specifies where an app-server review should run.
/// </summary>
public enum ReviewDelivery
{
    /// <summary>
    /// Run the review inline on the current thread.
    /// </summary>
    Inline,

    /// <summary>
    /// Run the review detached on a separate review thread.
    /// </summary>
    Detached
}

