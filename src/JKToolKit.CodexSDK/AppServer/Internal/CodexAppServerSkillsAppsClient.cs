using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerSkillsAppsClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;

    public CodexAppServerSkillsAppsClient(Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
    }

    public async Task<SkillsListResult> ListSkillsAsync(SkillsListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<string>? cwds = null;
        if (options.Cwds is { Count: > 0 })
        {
            cwds = options.Cwds;
        }
        else if (!string.IsNullOrWhiteSpace(options.Cwd))
        {
            cwds = [options.Cwd];
        }

        var result = await _sendRequestAsync(
            "skills/list",
            new UpstreamV2.SkillsListParams
            {
                Cwds = cwds?.ToArray(),
                ForceReload = options.ForceReload ? true : null
            },
            ct);

        var entries = CodexAppServerClientSkillsAppsParsers.ParseSkillsListEntries(result);

        return new SkillsListResult
        {
            Entries = entries,
            Skills = CodexAppServerClientSkillsAppsParsers.ParseSkillsListSkills(entries),
            Raw = result
        };
    }

    public async Task<SkillsExtraRootsSetResult> SetSkillsExtraRootsAsync(SkillsExtraRootsSetOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.ExtraRoots);

        for (var i = 0; i < options.ExtraRoots.Count; i++)
        {
            CodexAppServerPathValidation.ValidateRequiredAbsolutePath(
                options.ExtraRoots[i],
                nameof(options),
                $"{nameof(options.ExtraRoots)}[{i}]");
        }

        var result = await _sendRequestAsync(
            "skills/extraRoots/set",
            new UpstreamV2.SkillsExtraRootsSetParams
            {
                ExtraRoots = options.ExtraRoots.ToArray()
            },
            ct);

        return new SkillsExtraRootsSetResult
        {
            Raw = result
        };
    }

    public async Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.IsNullOrWhiteSpace(options.Cwd))
        {
            throw new ArgumentException("app/list does not support Cwd scoping on this upstream build. Use ThreadId instead.", nameof(options));
        }

        if (options.Limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Limit, "Limit cannot be negative.");
        }

        var result = await _sendRequestAsync(
            "app/list",
            new UpstreamV2.AppsListParams
            {
                Cursor = options.Cursor,
                Limit = options.Limit,
                ThreadId = options.ThreadId,
                ForceRefetch = options.ForceRefetch ? true : null
            },
            ct);

        return new AppsListResult
        {
            Apps = CodexAppServerClientSkillsAppsParsers.ParseAppsListApps(result),
            NextCursor = CodexAppServerClientThreadParsers.ExtractNextCursor(result),
            Raw = result
        };
    }

    public async Task<AppsReadResult> ReadAppsAsync(AppsReadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.AppIds);

        if (options.AppIds.Count == 0)
        {
            throw new ArgumentException("AppIds cannot be empty.", nameof(options));
        }

        if (options.AppIds.Count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.AppIds.Count, "AppIds cannot contain more than 100 entries.");
        }

        for (var i = 0; i < options.AppIds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(options.AppIds[i]))
            {
                throw new ArgumentException($"AppIds[{i}] cannot be empty or whitespace.", nameof(options));
            }
        }

        var result = await _sendRequestAsync(
            "app/read",
            new UpstreamV2.AppsReadParams
            {
                AppIds = options.AppIds.ToArray(),
                IncludeTools = options.IncludeTools ? true : null
            },
            ct);

        return new AppsReadResult
        {
            Apps = CodexAppServerClientSkillsAppsParsers.ParseAppsReadApps(result),
            MissingAppIds = CodexAppServerClientJson.GetOptionalStringArray(result, "missingAppIds") ?? Array.Empty<string>(),
            Raw = result
        };
    }

    public async Task<AppsInstalledResult> ReadInstalledAppsAsync(AppsInstalledOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "app/installed",
            new UpstreamV2.AppsInstalledParams
            {
                ThreadId = options.ThreadId,
                ForceRefresh = options.ForceRefresh ? true : null
            },
            ct);

        return new AppsInstalledResult
        {
            Apps = CodexAppServerClientSkillsAppsParsers.ParseInstalledApps(result),
            Raw = result
        };
    }
}
