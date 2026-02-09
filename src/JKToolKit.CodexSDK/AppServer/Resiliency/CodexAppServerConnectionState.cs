namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Represents the current connection state for a resilient app-server client.
/// </summary>
public enum CodexAppServerConnectionState
{
    /// <summary>
    /// The client is connected to a live app-server subprocess.
    /// </summary>
    Connected = 0,

    /// <summary>
    /// The client is currently attempting to restart the subprocess and reconnect.
    /// </summary>
    Restarting = 1,

    /// <summary>
    /// The client has permanently faulted (e.g. restart limit reached) and will not restart.
    /// </summary>
    Faulted = 2,

    /// <summary>
    /// The client has been disposed.
    /// </summary>
    Disposed = 3
}

