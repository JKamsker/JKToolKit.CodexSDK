namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Thrown when a resilient app-server client cannot connect (e.g. restart limit reached) and is faulted.
/// </summary>
public sealed class CodexAppServerUnavailableException : Exception
{
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public CodexAppServerUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

