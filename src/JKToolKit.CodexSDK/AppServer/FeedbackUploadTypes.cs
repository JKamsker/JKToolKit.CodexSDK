using System.Text.Json;

#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>feedback/upload</c>.
/// </summary>
public sealed class FeedbackUploadOptions
{
    /// <summary>
    /// Gets or sets the feedback classification.
    /// </summary>
    public required string Classification { get; set; }

    /// <summary>
    /// Gets or sets the optional user-facing reason.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the optional thread identifier associated with the feedback.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether current logs should be attached.
    /// </summary>
    public bool IncludeLogs { get; set; }

    /// <summary>
    /// Gets or sets optional extra log file paths to attach.
    /// </summary>
    public IReadOnlyList<string>? ExtraLogFiles { get; set; }
}

/// <summary>
/// Result returned by <c>feedback/upload</c>.
/// </summary>
public sealed record class FeedbackUploadResult
{
    public required string ThreadId { get; init; }
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>account/logout</c>.
/// </summary>
public sealed record class AccountLogoutResult
{
    public required JsonElement Raw { get; init; }
}

#pragma warning restore CS1591
