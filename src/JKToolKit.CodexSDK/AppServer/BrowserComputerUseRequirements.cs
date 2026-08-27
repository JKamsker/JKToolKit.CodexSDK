using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents in-app browser policy requirements.
/// </summary>
public sealed record class InAppBrowserRequirements
{
    /// <summary>
    /// Gets whether importing external browser settings is allowed.
    /// </summary>
    public bool? AllowExternalBrowserSettingsImport { get; init; }

    /// <summary>
    /// Gets the raw JSON requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents macOS computer-use app access requirements.
/// </summary>
public sealed record class ComputerUseMacosRequirements
{
    /// <summary>
    /// Gets app-access requirements keyed by bundle id.
    /// </summary>
    public IReadOnlyDictionary<string, AllowDenyRequirementValue>? BundleIds { get; init; }

    /// <summary>
    /// Gets the raw JSON requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents Windows computer-use app access requirements.
/// </summary>
public sealed record class ComputerUseWindowsRequirements
{
    /// <summary>
    /// Gets app-access requirements keyed by AUMID.
    /// </summary>
    public IReadOnlyDictionary<string, AllowDenyRequirementValue>? Aumids { get; init; }

    /// <summary>
    /// Gets app-access requirements keyed by publisher/product/binary metadata.
    /// </summary>
    public IReadOnlyList<ComputerUseWindowsExeRequirement>? Exes { get; init; }

    /// <summary>
    /// Gets the raw JSON requirements payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents a Windows executable computer-use requirement.
/// </summary>
public sealed record class ComputerUseWindowsExeRequirement
{
    /// <summary>
    /// Gets the executable publisher name.
    /// </summary>
    public required string PublisherName { get; init; }

    /// <summary>
    /// Gets the executable product name.
    /// </summary>
    public required string ProductName { get; init; }

    /// <summary>
    /// Gets the executable binary name, when upstream reports one.
    /// </summary>
    public string? BinaryName { get; init; }

    /// <summary>
    /// Gets the configured access requirement.
    /// </summary>
    public required AllowDenyRequirementValue Access { get; init; }

    /// <summary>
    /// Gets the raw JSON requirement payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents a browser-origin policy requirement.
/// </summary>
public sealed record class BrowserUseOriginPolicy
{
    /// <summary>
    /// Gets the page access requirement.
    /// </summary>
    public AllowDenyRequirementValue? Access { get; init; }

    /// <summary>
    /// Gets the downloads requirement.
    /// </summary>
    public AllowDenyRequirementValue? Downloads { get; init; }

    /// <summary>
    /// Gets the uploads requirement.
    /// </summary>
    public AllowDenyRequirementValue? Uploads { get; init; }

    /// <summary>
    /// Gets the full Chrome DevTools Protocol access requirement.
    /// </summary>
    public AllowDenyRequirementValue? FullCdpAccess { get; init; }

    /// <summary>
    /// Gets the auto-review requirement.
    /// </summary>
    public AllowDenyRequirementValue? AutoReview { get; init; }

    /// <summary>
    /// Gets whether persistent approval is allowed for this origin.
    /// </summary>
    public bool? PersistentApproval { get; init; }

    /// <summary>
    /// Gets the approval lifetime for browser-origin access.
    /// </summary>
    public BrowserUseAccessApprovalLifetimeValue? AccessApprovalLifetime { get; init; }

    /// <summary>
    /// Gets the raw JSON policy payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents an upstream allow/deny policy requirement.
/// </summary>
public readonly record struct AllowDenyRequirementValue
{
    private readonly string? _value;

    /// <summary>
    /// Gets the underlying wire value, or an empty string for an uninitialized value.
    /// </summary>
    public string Value => _value ?? string.Empty;

    private AllowDenyRequirementValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Requirement value cannot be empty or whitespace.", nameof(value));

        _value = value;
    }

    /// <summary>
    /// Gets the <c>allow</c> requirement.
    /// </summary>
    public static AllowDenyRequirementValue Allow => new("allow");

    /// <summary>
    /// Gets the <c>deny</c> requirement.
    /// </summary>
    public static AllowDenyRequirementValue Deny => new("deny");

    /// <summary>
    /// Parses an allow/deny requirement from a wire value.
    /// </summary>
    public static AllowDenyRequirementValue Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse an allow/deny requirement from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out AllowDenyRequirementValue requirement)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            requirement = default;
            return false;
        }

        requirement = new AllowDenyRequirementValue(value);
        return true;
    }

    /// <summary>
    /// Converts a string to an <see cref="AllowDenyRequirementValue"/>.
    /// </summary>
    public static implicit operator AllowDenyRequirementValue(string value) => Parse(value);

    /// <summary>
    /// Converts an <see cref="AllowDenyRequirementValue"/> to its wire value.
    /// </summary>
    public static implicit operator string(AllowDenyRequirementValue requirement) => requirement.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents browser-origin access approval lifetime.
/// </summary>
public readonly record struct BrowserUseAccessApprovalLifetimeValue
{
    private readonly string? _value;

    /// <summary>
    /// Gets the underlying wire value, or an empty string for an uninitialized value.
    /// </summary>
    public string Value => _value ?? string.Empty;

    private BrowserUseAccessApprovalLifetimeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Approval lifetime cannot be empty or whitespace.", nameof(value));

        _value = value;
    }

    /// <summary>
    /// Gets the <c>turn</c> lifetime.
    /// </summary>
    public static BrowserUseAccessApprovalLifetimeValue Turn => new("turn");

    /// <summary>
    /// Gets the <c>thread</c> lifetime.
    /// </summary>
    public static BrowserUseAccessApprovalLifetimeValue Thread => new("thread");

    /// <summary>
    /// Parses an approval lifetime from a wire value.
    /// </summary>
    public static BrowserUseAccessApprovalLifetimeValue Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse an approval lifetime from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out BrowserUseAccessApprovalLifetimeValue lifetime)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            lifetime = default;
            return false;
        }

        lifetime = new BrowserUseAccessApprovalLifetimeValue(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="BrowserUseAccessApprovalLifetimeValue"/>.
    /// </summary>
    public static implicit operator BrowserUseAccessApprovalLifetimeValue(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="BrowserUseAccessApprovalLifetimeValue"/> to its wire value.
    /// </summary>
    public static implicit operator string(BrowserUseAccessApprovalLifetimeValue lifetime) => lifetime.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
