using JKToolKit.CodexSDK.AppServer.Remote;

namespace JKToolKit.CodexSDK.AgentFramework.Agents.Remote;

/// <summary>
/// Configures a Codex Agent Framework agent to attach to a SDK-managed remote app-server.
/// </summary>
public sealed class CodexAgentRemoteAppServerOptions
{
    /// <summary>
    /// Gets or sets the remote app-server manager that owns the registry entry.
    /// </summary>
    public required CodexRemoteAppServerManager Manager { get; set; }

    /// <summary>
    /// Gets or sets the registry entry id to attach for each agent run.
    /// </summary>
    public required string EntryId { get; set; }

    /// <summary>
    /// Gets or sets optional attach options such as runtime SSH password, bearer token, or client options.
    /// </summary>
    public CodexRemoteAttachOptions? AttachOptions { get; set; }
}
