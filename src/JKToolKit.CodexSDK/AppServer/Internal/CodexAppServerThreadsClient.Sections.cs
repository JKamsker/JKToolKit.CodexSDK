using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerThreadsClient
{
    public async Task<ThreadSectionListPage> ListThreadSectionsAsync(ThreadSectionListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "threadSection/list",
            new
            {
                options.Cursor,
                options.Limit
            },
            ct);

        return CodexAppServerClientThreadParsers.ParseThreadSectionListPage(result);
    }

    public async Task<ThreadSectionResult> CreateThreadSectionAsync(ThreadSectionCreateOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRequiredWireValue(options.Name, nameof(options.Name), nameof(options));

        var result = await _sendRequestAsync(
            "threadSection/create",
            new { options.Name },
            ct);

        return CodexAppServerClientThreadParsers.ParseThreadSectionResult(result, "threadSection/create");
    }

    public async Task<ThreadSectionResult> UpdateThreadSectionAsync(ThreadSectionUpdateOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRequiredWireValue(options.SectionId, nameof(options.SectionId), nameof(options));
        ValidateRequiredWireValue(options.Name, nameof(options.Name), nameof(options));

        var result = await _sendRequestAsync(
            "threadSection/update",
            new
            {
                options.SectionId,
                options.Name
            },
            ct);

        return CodexAppServerClientThreadParsers.ParseThreadSectionResult(result, "threadSection/update");
    }

    public async Task<ThreadSectionResult> DeleteThreadSectionAsync(ThreadSectionDeleteOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRequiredWireValue(options.SectionId, nameof(options.SectionId), nameof(options));

        var result = await _sendRequestAsync(
            "threadSection/delete",
            new { options.SectionId },
            ct);

        return new ThreadSectionResult
        {
            Raw = result
        };
    }

    public async Task<ThreadSectionResult> MoveThreadToSectionAsync(ThreadSectionMoveOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRequiredWireValue(options.ThreadId, nameof(options.ThreadId), nameof(options));
        ValidateOptionalWireValue(options.SectionId, nameof(options.SectionId), nameof(options));
        ValidateOptionalWireValue(options.BeforeThreadId, nameof(options.BeforeThreadId), nameof(options));

        var result = await _sendRequestAsync(
            "thread/section/move",
            BuildThreadSectionMoveParams(options),
            ct);

        return new ThreadSectionResult
        {
            Raw = result
        };
    }

    private static Dictionary<string, object?> BuildThreadSectionMoveParams(ThreadSectionMoveOptions options)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["threadId"] = options.ThreadId,
            ["sectionId"] = options.SectionId
        };

        if (options.BeforeThreadId is not null)
        {
            values["beforeThreadId"] = options.BeforeThreadId;
        }

        return values;
    }

    private static JsonElement? BuildThreadListSectionId(ThreadListOptions options)
    {
        ValidateOptionalWireValue(options.SectionId, nameof(options.SectionId), nameof(options));

        if (options.UnsectionedOnly && !string.IsNullOrWhiteSpace(options.SectionId))
        {
            throw new ArgumentException("SectionId and UnsectionedOnly cannot both be set.", nameof(options));
        }

        if (options.UnsectionedOnly)
        {
            return JsonSerializer.SerializeToElement((string?)null);
        }

        if (!string.IsNullOrWhiteSpace(options.SectionId))
        {
            return JsonSerializer.SerializeToElement(options.SectionId);
        }

        return null;
    }

    private static void ValidateOptionalWireValue(string? value, string displayName, string paramName)
    {
        if (value is null)
        {
            return;
        }

        ValidateRequiredWireValue(value, displayName, paramName);
    }

    private static void ValidateRequiredWireValue(string? value, string displayName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} cannot be empty or whitespace.", paramName);
        }
    }
}
