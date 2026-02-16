using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

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

        SkillsListExtraRootsForCwd[]? perCwd = null;
        if (options.ExtraRootsForCwd is { Count: > 0 })
        {
            var cwd = options.Cwd ?? (cwds is { Count: 1 } ? cwds[0] : null);
            if (string.IsNullOrWhiteSpace(cwd))
            {
                throw new ArgumentException("ExtraRootsForCwd requires a single Cwd scope.", nameof(options));
            }

            perCwd =
            [
                new SkillsListExtraRootsForCwd
                {
                    Cwd = cwd,
                    ExtraUserRoots = options.ExtraRootsForCwd
                }
            ];
        }

        var result = await _sendRequestAsync(
            "skills/list",
            new SkillsListParams
            {
                Cwds = cwds,
                ForceReload = options.ForceReload ? true : null,
                PerCwdExtraUserRoots = perCwd
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

    public async Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "app/list",
            new AppListParams
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
}
