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
    /// Gets the allow-list of sandbox modes, when present.
    /// </summary>
    public IReadOnlyList<CodexSandboxMode>? AllowedSandboxModes { get; init; }

    /// <summary>
    /// Gets the allow-list of web search modes, when present.
    /// </summary>
    public IReadOnlyList<CodexWebSearchMode>? AllowedWebSearchModes { get; init; }

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
    /// Gets the raw JSON requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

