using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerThreadAttachmentParsers
{
    public static ThreadAttachmentAddResult ParseAddResult(JsonElement result)
    {
        var attachment = CodexAppServerClientJson.TryGetObject(result, "attachment")
            ?? throw new InvalidOperationException("Missing required property 'attachment' on thread/attachment/add response.");

        return new ThreadAttachmentAddResult
        {
            Outcome = ParseAddOutcome(CodexAppServerClientJson.GetStringOrNull(result, JsonFieldNames.Outcome)),
            Attachment = ParseAttachment(attachment),
            Raw = result
        };
    }

    public static ThreadAttachmentListPage ParseListPage(JsonElement result)
    {
        var data = CodexAppServerClientJson.TryGetArray(result, JsonFieldNames.Data)
            ?? throw new InvalidOperationException("Missing required property 'data' on thread/attachment/list response.");
        var attachments = new List<ThreadAttachmentInfo>();

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("thread/attachment/list response contains a non-object entry in 'data'.");
            }

            attachments.Add(ParseAttachment(item));
        }

        return new ThreadAttachmentListPage
        {
            Attachments = attachments,
            NextCursor = CodexAppServerClientJson.GetStringOrNull(result, JsonFieldNames.NextCursor),
            Raw = result
        };
    }

    private static ThreadAttachmentInfo ParseAttachment(JsonElement attachment)
    {
        if (!attachment.TryGetProperty(JsonFieldNames.Payload, out var payload))
        {
            throw new InvalidOperationException("Missing required property 'payload' on thread attachment.");
        }

        return new ThreadAttachmentInfo
        {
            Id = CodexAppServerClientJson.GetRequiredString(attachment, JsonFieldNames.Id, "thread attachment"),
            AttachmentType = CodexAppServerClientJson.GetRequiredString(attachment, JsonFieldNames.AttachmentType, "thread attachment"),
            IdentityKey = CodexAppServerClientJson.GetRequiredString(attachment, JsonFieldNames.IdentityKey, "thread attachment"),
            Payload = payload.Clone(),
            CreatedAt = CodexAppServerClientJson.GetRequiredInt64(attachment, JsonFieldNames.CreatedAt, "thread attachment"),
            Raw = attachment.Clone()
        };
    }

    private static ThreadAttachmentAddOutcome ParseAddOutcome(string? outcome) =>
        outcome switch
        {
            "created" => ThreadAttachmentAddOutcome.Created,
            "existing" => ThreadAttachmentAddOutcome.Existing,
            _ => ThreadAttachmentAddOutcome.Unknown
        };
}
