namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Lists available collaboration mode presets (experimental).
    /// </summary>
    public Task<CollaborationModeListResult> ListCollaborationModesAsync(CancellationToken ct = default) =>
        _collaborationModesClient.ListCollaborationModesAsync(ct);
}
