using System.Text.Json;
using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerThreadsClient
{
    public async Task<ThreadAttachmentAddResult> AddThreadAttachmentAsync(
        ThreadAttachmentAddOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateAttachmentIdentity(options.ThreadId, options.AttachmentType, options.IdentityKey, nameof(options));
        if (options.Payload.ValueKind == JsonValueKind.Undefined)
            throw new ArgumentException("Payload must contain a JSON value.", nameof(options));

        var result = await _sendRequestAsync(
            "thread/attachment/add",
            new UpstreamV2.ThreadAttachmentAddParams
            {
                ThreadId = options.ThreadId,
                AttachmentType = options.AttachmentType,
                IdentityKey = options.IdentityKey,
                Payload = options.Payload
            },
            ct).ConfigureAwait(false);

        return CodexAppServerThreadAttachmentParsers.ParseAddResult(result);
    }

    public async Task<ThreadAttachmentListPage> ListThreadAttachmentsAsync(
        ThreadAttachmentListOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "thread/attachment/list",
            new UpstreamV2.ThreadAttachmentListParams
            {
                ThreadId = options.ThreadId,
                Cursor = options.Cursor,
                Limit = options.Limit
            },
            ct).ConfigureAwait(false);

        return CodexAppServerThreadAttachmentParsers.ParseListPage(result);
    }

    public async Task<ThreadAttachmentRemoveResult> RemoveThreadAttachmentAsync(
        ThreadAttachmentRemoveOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateAttachmentIdentity(options.ThreadId, options.AttachmentType, options.IdentityKey, nameof(options));

        var result = await _sendRequestAsync(
            "thread/attachment/remove",
            new UpstreamV2.ThreadAttachmentRemoveParams
            {
                ThreadId = options.ThreadId,
                AttachmentType = options.AttachmentType,
                IdentityKey = options.IdentityKey
            },
            ct).ConfigureAwait(false);

        return new ThreadAttachmentRemoveResult { Raw = result };
    }

    private static void ValidateAttachmentIdentity(
        string threadId,
        string attachmentType,
        string identityKey,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", parameterName);
        if (string.IsNullOrWhiteSpace(attachmentType))
            throw new ArgumentException("AttachmentType cannot be empty or whitespace.", parameterName);
        if (string.IsNullOrWhiteSpace(identityKey))
            throw new ArgumentException("IdentityKey cannot be empty or whitespace.", parameterName);
    }
}
