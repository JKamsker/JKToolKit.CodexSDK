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
            BuildThreadSectionCreateParams(options),
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
            BuildThreadSectionUpdateParams(options),
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

    private static Dictionary<string, object?> BuildThreadSectionCreateParams(ThreadSectionCreateOptions options)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = options.Name
        };

        if (options.Appearance is not null)
        {
            values["appearance"] = BuildThreadSectionAppearance(options.Appearance);
        }

        return values;
    }

    private static Dictionary<string, object?> BuildThreadSectionUpdateParams(ThreadSectionUpdateOptions options)
    {
        if (options.Appearance is not null && options.ClearAppearance)
        {
            throw new ArgumentException("Appearance and ClearAppearance cannot both be set.", nameof(options));
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sectionId"] = options.SectionId,
            ["name"] = options.Name
        };

        if (options.ClearAppearance)
        {
            values["appearance"] = null;
        }
        else if (options.Appearance is not null)
        {
            values["appearance"] = BuildThreadSectionAppearance(options.Appearance);
        }

        return values;
    }

    private static Dictionary<string, object?> BuildThreadSectionAppearance(ThreadSectionAppearanceOptions appearance) =>
        new(StringComparer.Ordinal)
        {
            ["color"] = appearance.Color,
            ["icon"] = appearance.Icon
        };

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

    private static JsonElement? BuildThreadListProjectId(ThreadListOptions options, bool experimentalApiEnabled)
    {
        ValidateOptionalWireValue(options.ProjectId, nameof(options.ProjectId), nameof(options));

        if (options.UnassignedProjectOnly && !string.IsNullOrWhiteSpace(options.ProjectId))
        {
            throw new ArgumentException("ProjectId and UnassignedProjectOnly cannot both be set.", nameof(options));
        }

        if ((options.UnassignedProjectOnly || !string.IsNullOrWhiteSpace(options.ProjectId)) && !experimentalApiEnabled)
        {
            throw new CodexExperimentalApiRequiredException("thread/list.projectId");
        }

        if (options.UnassignedProjectOnly)
        {
            return JsonSerializer.SerializeToElement((string?)null);
        }

        if (!string.IsNullOrWhiteSpace(options.ProjectId))
        {
            return JsonSerializer.SerializeToElement(options.ProjectId);
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
