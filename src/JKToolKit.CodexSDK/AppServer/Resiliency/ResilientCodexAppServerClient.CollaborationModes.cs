#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<CollaborationModeListResult> ListCollaborationModesAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.CollaborationModes, (c, token) => c.ListCollaborationModesAsync(token), ct);
}

#pragma warning restore CS1591
