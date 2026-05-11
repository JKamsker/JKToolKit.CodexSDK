using System.Text.Json;
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
}
