using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal sealed partial class CodexAppServerConfigClient
{
    public async Task<ConversationSummaryResult> GetConversationSummaryAsync(ConversationSummaryOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var hasConversationId = !string.IsNullOrWhiteSpace(options.ConversationId);
        var hasRolloutPath = !string.IsNullOrWhiteSpace(options.RolloutPath);
        if (hasConversationId == hasRolloutPath)
        {
            throw new ArgumentException(
                "Exactly one of ConversationId or RolloutPath must be provided.",
                nameof(options));
        }

        object @params;
        if (hasConversationId)
        {
            @params = new
            {
                conversationId = options.ConversationId
            };
        }
        else
        {
            CodexAppServerPathValidation.ValidateRequiredAbsolutePath(options.RolloutPath, nameof(options), "RolloutPath");
            @params = new
            {
                rolloutPath = options.RolloutPath
            };
        }

        var result = await _sendRequestAsync(
            "getConversationSummary",
            @params,
            ct);

        return new ConversationSummaryResult
        {
            Summary = ParseConversationSummary(result),
            Raw = result
        };
    }

    public async Task<GitDiffToRemoteResult> GetGitDiffToRemoteAsync(GitDiffToRemoteOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        CodexAppServerPathValidation.ValidateRequiredAbsolutePath(options.Cwd, nameof(options), "Cwd");

        var result = await _sendRequestAsync(
            "gitDiffToRemote",
            new
            {
                cwd = options.Cwd
            },
            ct);

        return new GitDiffToRemoteResult
        {
            Sha = GetRequiredString(result, "sha", "gitDiffToRemote response"),
            Diff = GetRequiredString(result, "diff", "gitDiffToRemote response"),
            Raw = result
        };
    }

    public async Task<AuthStatusReadResult> GetAuthStatusAsync(AuthStatusOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "getAuthStatus",
            new
            {
                includeToken = options.IncludeToken,
                refreshToken = options.RefreshToken
            },
            ct);

        return new AuthStatusReadResult
        {
            AuthMethod = CodexAppServerAccountParsers.ParseAuthModeOrNull(result, "authMethod", "getAuthStatus response"),
            AuthToken = GetStringOrNull(result, "authToken"),
            RequiresOpenaiAuth = GetBoolOrNull(result, "requiresOpenaiAuth"),
            Raw = result
        };
    }

    private static CodexConversationSummary ParseConversationSummary(JsonElement result)
    {
        var summary = TryGetObject(result, "summary")
            ?? throw new InvalidOperationException("Missing required object property 'summary' on getConversationSummary response.");

        var context = "getConversationSummary response.summary";
        return new CodexConversationSummary
        {
            ConversationId = GetRequiredString(summary, "conversationId", context),
            Path = CodexAppServerPathValidation.RequireAbsolutePayloadPath(
                GetRequiredString(summary, "path", context),
                "path",
                context),
            Preview = GetRequiredString(summary, "preview", context),
            Timestamp = GetStringOrNull(summary, "timestamp"),
            UpdatedAt = GetStringOrNull(summary, "updatedAt"),
            ModelProvider = GetRequiredString(summary, "modelProvider", context),
            Cwd = CodexAppServerPathValidation.RequireAbsolutePayloadPath(
                GetRequiredString(summary, "cwd", context),
                "cwd",
                context),
            CliVersion = GetRequiredString(summary, "cliVersion", context),
            Source = GetRequiredString(summary, "source", context),
            GitInfo = ParseConversationGitInfoOrNull(summary, context),
            Raw = summary.Clone()
        };
    }

    private static CodexThreadGitInfo? ParseConversationGitInfoOrNull(JsonElement summary, string context)
    {
        var gitInfo = TryGetObject(summary, "gitInfo");
        if (!gitInfo.HasValue)
        {
            return null;
        }

        return new CodexThreadGitInfo
        {
            Sha = GetStringOrNull(gitInfo.Value, "sha"),
            Branch = GetStringOrNull(gitInfo.Value, "branch"),
            OriginUrl = GetStringOrNull(gitInfo.Value, "originUrl"),
            Raw = gitInfo.Value.Clone()
        };
    }
}
