namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Thrown when a caller attempts to use an app-server field or method that is gated behind the experimental API capability.
/// </summary>
public sealed class CodexExperimentalApiRequiredException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodexExperimentalApiRequiredException"/> class.
    /// </summary>
    public CodexExperimentalApiRequiredException(string descriptor)
        : base($"'{descriptor}' requires Codex app-server experimental API capability (initialize.params.capabilities.experimentalApi = true).")
    {
        Descriptor = descriptor;
    }

    /// <summary>
    /// Gets the upstream descriptor for the experimental-gated field/method (e.g. <c>turn/start.collaborationMode</c>).
    /// </summary>
    public string Descriptor { get; }
}

