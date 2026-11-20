using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerThreadsClient
{
    public async Task<CodexThreadListPage> ListThreadsAsync(ThreadListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ExperimentalApiGuards.ValidateThreadList(options, _experimentalApiEnabled());

        var result = await _sendRequestAsync(
            "thread/list",
            new ThreadListParams
            {
                CurrentAccountOnly = options.CurrentAccountOnly,
                Archived = options.Archived,
                SectionId = BuildThreadListSectionId(options),
                ProjectId = BuildThreadListProjectId(options, _experimentalApiEnabled()),
                Cwd = options.Cwd,
                Limit = options.Limit,
                ExcludedThreadIds = options.ExcludedThreadIds,
                IncludedThreadIds = options.IncludedThreadIds,
                ModelProviders = options.ModelProviders,
                SearchTerm = options.SearchTerm,
                SourceKinds = options.SourceKinds,
                Originators = options.Originators,
                AttachedToThreadId = options.AttachedToThreadId,
                Cursor = options.Cursor,
                SortKey = options.SortKey,
            },
            ct);

        return new CodexThreadListPage
        {
            Threads = CodexAppServerClientThreadParsers.ParseThreadListThreads(result),
            NextCursor = CodexAppServerClientThreadParsers.ExtractNextCursor(result),
            Raw = result
        };
    }
}
