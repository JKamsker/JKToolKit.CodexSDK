using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AgentFramework.Tools;

/// <summary>
/// Contains Codex app-server dynamic tools and the matching server-request handler for Agent Framework functions.
/// </summary>
public sealed class AgentFrameworkCodexToolSet
{
    /// <summary>
    /// Creates an Agent Framework tool set for Codex app-server dynamic tools.
    /// </summary>
    /// <param name="dynamicTools">The dynamic tool specifications to pass to <see cref="ThreadStartOptions.DynamicTools"/>.</param>
    /// <param name="approvalHandler">The handler to assign to <see cref="CodexAppServerClientOptions.ApprovalHandler"/>.</param>
    /// <param name="toolSchemaHash">The stable hash of the dynamic tool schema, if tools are present.</param>
    public AgentFrameworkCodexToolSet(
        IReadOnlyList<DynamicToolSpec> dynamicTools,
        IAppServerApprovalHandler approvalHandler,
        string? toolSchemaHash = null)
    {
        ArgumentNullException.ThrowIfNull(dynamicTools);
        ArgumentNullException.ThrowIfNull(approvalHandler);

        DynamicTools = dynamicTools;
        ApprovalHandler = approvalHandler;
        ToolSchemaHash = toolSchemaHash;
    }

    /// <summary>
    /// Gets the dynamic tool specifications to pass to <see cref="ThreadStartOptions.DynamicTools"/>.
    /// </summary>
    public IReadOnlyList<DynamicToolSpec> DynamicTools { get; }

    /// <summary>
    /// Gets the server-request handler that invokes Agent Framework functions for <c>item/tool/call</c> requests.
    /// </summary>
    public IAppServerApprovalHandler ApprovalHandler { get; }

    /// <summary>
    /// Gets the stable hash of the dynamic tool schema, if tools are present.
    /// </summary>
    public string? ToolSchemaHash { get; }
}
