using System.Text.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents policy/requirements constraints loaded by Codex (for example from <c>requirements.toml</c> or MDM).
/// </summary>
public sealed record class ConfigRequirements
{
    /// <summary>
    /// Gets the allow-list of approval policies, when present.
    /// </summary>
    public IReadOnlyList<CodexApprovalPolicy>? AllowedApprovalPolicies { get; init; }

    /// <summary>
    /// Gets the allow-list of approval policies expressed as <c>AskForApproval</c> union values, when present.
    /// </summary>
    public IReadOnlyList<CodexAskForApproval>? AllowedAskForApproval { get; init; }

    /// <summary>
    /// Gets the allow-list of approval reviewers, when present.
    /// </summary>
    public IReadOnlyList<CodexApprovalsReviewer>? AllowedApprovalsReviewers { get; init; }

    /// <summary>
    /// Gets the allow-list of sandbox modes, when present.
    /// </summary>
    public IReadOnlyList<CodexSandboxMode>? AllowedSandboxModes { get; init; }

    /// <summary>
    /// Gets the allow-list of Windows sandbox setup implementations, when present.
    /// </summary>
    public IReadOnlyList<WindowsSandboxSetupMode>? AllowedWindowsSandboxImplementations { get; init; }

    /// <summary>
    /// Gets the named permission-profile allow-list keyed by profile id, when present.
    /// </summary>
    public IReadOnlyDictionary<string, bool>? AllowedPermissionProfiles { get; init; }

    /// <summary>
    /// Gets the allow-list of named permission profile ids, when present.
    /// </summary>
    public IReadOnlyList<string>? AllowedPermissionProfileIds { get; init; }

    /// <summary>
    /// Gets the default permission profile id, when present.
    /// </summary>
    public string? DefaultPermissionProfileId { get; init; }

    /// <summary>
    /// Gets the allow-list of web search modes, when present.
    /// </summary>
    public IReadOnlyList<CodexWebSearchMode>? AllowedWebSearchModes { get; init; }

    /// <summary>
    /// Gets feature-gating requirements by feature name, when present.
    /// </summary>
    public IReadOnlyDictionary<string, bool>? FeatureRequirements { get; init; }

    /// <summary>
    /// Gets whether unmanaged hooks are disabled while managed hook requirements are active.
    /// </summary>
    public bool? AllowManagedHooksOnly { get; init; }

    /// <summary>
    /// Gets whether app snapshots are allowed by policy.
    /// </summary>
    public bool? AllowAppshots { get; init; }

    /// <summary>
    /// Gets whether login shells are allowed by policy.
    /// </summary>
    public bool? AllowLoginShell { get; init; }

    /// <summary>
    /// Gets the managed CLI auth credentials store mode, when present.
    /// </summary>
    public CliAuthCredentialsStoreMode? CliAuthCredentialsStore { get; init; }

    /// <summary>
    /// Gets the managed ChatGPT base URL, when present.
    /// </summary>
    public string? ChatGptBaseUrl { get; init; }

    /// <summary>
    /// Gets whether update checks should run on startup.
    /// </summary>
    public bool? CheckForUpdateOnStartup { get; init; }

    /// <summary>
    /// Gets whether the Windows sandbox should use a private desktop.
    /// </summary>
    public bool? WindowsSandboxPrivateDesktop { get; init; }

    /// <summary>
    /// Gets computer-use requirements, when present.
    /// </summary>
    public ComputerUseRequirements? ComputerUse { get; init; }

    /// <summary>
    /// Gets browser-use requirements, when present.
    /// </summary>
    public BrowserUseRequirements? BrowserUse { get; init; }

    /// <summary>
    /// Gets feedback requirements, when present.
    /// </summary>
    public FeedbackRequirements? Feedback { get; init; }

    /// <summary>
    /// Gets the SQLite home path URI, when enforced by policy.
    /// </summary>
    public string? SqliteHome { get; init; }

    /// <summary>
    /// Gets the log directory path URI, when enforced by policy.
    /// </summary>
    public string? LogDir { get; init; }

    /// <summary>
    /// Gets the model catalog JSON path URI, when enforced by policy.
    /// </summary>
    public string? ModelCatalogJson { get; init; }

    /// <summary>
    /// Gets managed hook requirements as raw JSON, when present.
    /// </summary>
    public JsonElement? Hooks { get; init; }

    /// <summary>
    /// Gets the enforced residency requirement, when present.
    /// </summary>
    public CodexResidencyRequirement? EnforceResidency { get; init; }

    /// <summary>
    /// Gets network requirements/proxy details, when present.
    /// </summary>
    /// <remarks>
    /// Upstream may gate this field behind experimental API capabilities.
    /// </remarks>
    public NetworkRequirements? Network { get; init; }

    /// <summary>
    /// Gets automatic review requirements, when present.
    /// </summary>
    public AutoReviewRequirements? AutoReview { get; init; }

    /// <summary>
    /// Gets the raw JSON requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents automatic review policy requirements.
/// </summary>
public sealed record class AutoReviewRequirements
{
    /// <summary>
    /// Gets model ids that require automatic review, when present.
    /// </summary>
    public IReadOnlyList<string>? RequiredOnModels { get; init; }

    /// <summary>
    /// Gets automatic-review ignore rules, when present.
    /// </summary>
    public IReadOnlyList<string>? IgnoreRules { get; init; }

    /// <summary>
    /// Gets the raw JSON requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents policy gates for computer-use flows.
/// </summary>
public sealed record class ComputerUseRequirements
{
    /// <summary>
    /// Gets whether locked-computer use is allowed.
    /// </summary>
    public bool? AllowLockedComputerUse { get; init; }

    /// <summary>
    /// Gets the raw JSON requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents policy gates for browser-use flows.
/// </summary>
public sealed record class BrowserUseRequirements
{
    /// <summary>
    /// Gets whether browser-use auto review should be disabled.
    /// </summary>
    public bool? DisableAutoReview { get; init; }

    /// <summary>
    /// Gets the raw JSON requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents policy gates for feedback upload flows.
/// </summary>
public sealed record class FeedbackRequirements
{
    /// <summary>
    /// Gets whether feedback uploads are enabled.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Gets the raw JSON requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
