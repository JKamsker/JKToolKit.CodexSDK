namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<CollaborationModeListResult> ListCollaborationModesAsync(CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<CollaborationModeListResult> ListCollaborationModesAsync(CancellationToken ct) => _inner.ListCollaborationModesAsync(ct);
}
