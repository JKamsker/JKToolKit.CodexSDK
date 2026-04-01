using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Selects which conversation summary the app-server should return.
/// </summary>
public sealed class ConversationSummaryOptions
{
    /// <summary>
    /// Gets or sets the conversation/thread identifier to resolve.
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the rollout log path to resolve.
    /// </summary>
    public string? RolloutPath { get; set; }
}

/// <summary>
/// Represents the summary returned by <c>getConversationSummary</c>.
/// </summary>
public sealed record class CodexConversationSummary
{
    /// <summary>
    /// Gets the conversation identifier.
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// Gets the absolute rollout log path for the conversation.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the short preview text.
    /// </summary>
    public required string Preview { get; init; }

    /// <summary>
    /// Gets the legacy timestamp value when present.
    /// </summary>
    public string? Timestamp { get; init; }

    /// <summary>
    /// Gets the last-updated timestamp when present.
    /// </summary>
    public string? UpdatedAt { get; init; }

    /// <summary>
    /// Gets the model provider associated with the conversation.
    /// </summary>
    public required string ModelProvider { get; init; }

    /// <summary>
    /// Gets the absolute working directory associated with the conversation.
    /// </summary>
    public required string Cwd { get; init; }

    /// <summary>
    /// Gets the CLI version that produced the conversation.
    /// </summary>
    public required string CliVersion { get; init; }

    /// <summary>
    /// Gets the upstream session source wire value.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets optional Git metadata associated with the conversation.
    /// </summary>
    public CodexThreadGitInfo? GitInfo { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the summary object.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents the result returned by <c>getConversationSummary</c>.
/// </summary>
public sealed record class ConversationSummaryResult
{
    /// <summary>
    /// Gets the parsed summary.
    /// </summary>
    public required CodexConversationSummary Summary { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>gitDiffToRemote</c>.
/// </summary>
public sealed class GitDiffToRemoteOptions
{
    /// <summary>
    /// Gets or sets the repository working directory to diff.
    /// </summary>
    public string? Cwd { get; set; }
}

/// <summary>
/// Represents the result returned by <c>gitDiffToRemote</c>.
/// </summary>
public sealed record class GitDiffToRemoteResult
{
    /// <summary>
    /// Gets the remote commit SHA used as the diff base.
    /// </summary>
    public required string Sha { get; init; }

    /// <summary>
    /// Gets the unified diff content.
    /// </summary>
    public required string Diff { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>getAuthStatus</c>.
/// </summary>
public sealed class AuthStatusOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the server should include the current auth token when available.
    /// </summary>
    public bool? IncludeToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the server should proactively refresh managed tokens before reporting status.
    /// </summary>
    public bool? RefreshToken { get; set; }
}

/// <summary>
/// Represents the result returned by <c>getAuthStatus</c>.
/// </summary>
public sealed record class AuthStatusReadResult
{
    /// <summary>
    /// Gets the reported auth method when present.
    /// </summary>
    public CodexAuthMode? AuthMethod { get; init; }

    /// <summary>
    /// Gets the current auth token when included by the server.
    /// </summary>
    public string? AuthToken { get; init; }

    /// <summary>
    /// Gets a value indicating whether OpenAI auth is currently required.
    /// </summary>
    public bool? RequiresOpenaiAuth { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
