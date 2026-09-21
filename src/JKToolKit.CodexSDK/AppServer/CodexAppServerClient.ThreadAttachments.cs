namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Creates or locates an attachment on its owning thread.
    /// </summary>
    public Task<ThreadAttachmentAddResult> AddThreadAttachmentAsync(
        ThreadAttachmentAddOptions options,
        CancellationToken ct = default) =>
        _threadsClient.AddThreadAttachmentAsync(options, ct);

    /// <summary>
    /// Lists a page of attachments associated with a thread.
    /// </summary>
    public Task<ThreadAttachmentListPage> ListThreadAttachmentsAsync(
        ThreadAttachmentListOptions options,
        CancellationToken ct = default) =>
        _threadsClient.ListThreadAttachmentsAsync(options, ct);

    /// <summary>
    /// Removes an attachment by its stable thread-local identity.
    /// </summary>
    public Task<ThreadAttachmentRemoveResult> RemoveThreadAttachmentAsync(
        ThreadAttachmentRemoveOptions options,
        CancellationToken ct = default) =>
        _threadsClient.RemoveThreadAttachmentAsync(options, ct);
}
