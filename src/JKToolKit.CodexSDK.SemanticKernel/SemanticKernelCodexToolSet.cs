using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.SemanticKernel;

/// <summary>
/// Contains Codex app-server dynamic tools and the matching server-request handler for Semantic Kernel functions.
/// </summary>
public sealed class SemanticKernelCodexToolSet
{
    /// <summary>
    /// Creates a Semantic Kernel tool set for Codex app-server dynamic tools.
    /// </summary>
    /// <param name="dynamicTools">The dynamic tool specifications to pass to <see cref="ThreadStartOptions.DynamicTools"/>.</param>
    /// <param name="approvalHandler">The handler to assign to <see cref="CodexAppServerClientOptions.ApprovalHandler"/>.</param>
    public SemanticKernelCodexToolSet(
        IReadOnlyList<DynamicToolSpec> dynamicTools,
        IAppServerApprovalHandler approvalHandler)
    {
        ArgumentNullException.ThrowIfNull(dynamicTools);
        ArgumentNullException.ThrowIfNull(approvalHandler);

        DynamicTools = dynamicTools;
        ApprovalHandler = approvalHandler;
    }

    /// <summary>
    /// Gets the dynamic tool specifications to pass to <see cref="ThreadStartOptions.DynamicTools"/>.
    /// </summary>
    public IReadOnlyList<DynamicToolSpec> DynamicTools { get; }

    /// <summary>
    /// Gets the server-request handler that invokes Semantic Kernel functions for <c>item/tool/call</c> requests.
    /// </summary>
    public IAppServerApprovalHandler ApprovalHandler { get; }
}
