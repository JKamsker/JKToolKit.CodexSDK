namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Known stream labels for <c>command/exec/outputDelta</c> notifications.
/// </summary>
public enum CommandExecOutputStreamKind
{
    /// <summary>
    /// The upstream stream label was absent or unrecognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Standard output.
    /// </summary>
    Stdout = 1,

    /// <summary>
    /// Standard error.
    /// </summary>
    Stderr = 2
}
