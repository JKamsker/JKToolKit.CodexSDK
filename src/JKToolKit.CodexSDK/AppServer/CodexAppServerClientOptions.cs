using System.Text.Json;
using JKToolKit.CodexSDK.Exec;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for launching and configuring a <see cref="CodexAppServerClient"/>.
/// </summary>
public sealed class CodexAppServerClientOptions
{
    /// <summary>
    /// Gets or sets the process launch configuration for the Codex executable.
    /// </summary>
    public CodexLaunch Launch { get; set; } = CodexLaunch.CodexOnPath().WithArgs("app-server");

    /// <summary>
    /// Gets or sets an optional explicit path to the Codex executable.
    /// </summary>
    public string? CodexExecutablePath { get; set; }

    /// <summary>
    /// Gets or sets the Codex home directory (passed as <c>CODEX_HOME</c> to the launched process).
    /// </summary>
    public string? CodexHomeDirectory { get; set; }

    /// <summary>
    /// Gets or sets the timeout for the app-server startup handshake.
    /// </summary>
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout used when shutting down the app-server process.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the default client info sent during initialization.
    /// </summary>
    public AppServerClientInfo DefaultClientInfo { get; set; } = new(
        "ncodexsdk",
        "JKToolKit.CodexSDK",
        "1.0.0");

    /// <summary>
    /// Gets or sets an optional override for JSON serialization options used by the client.
    /// </summary>
    public JsonSerializerOptions? SerializerOptionsOverride { get; set; }

    /// <summary>
    /// Gets or sets the size of the internal notifications buffer.
    /// </summary>
    public int NotificationBufferCapacity { get; set; } = 5000;

    /// <summary>
    /// Gets or sets an optional handler for approval requests coming from the app server.
    /// </summary>
    public IAppServerApprovalHandler? ApprovalHandler { get; set; }
}
