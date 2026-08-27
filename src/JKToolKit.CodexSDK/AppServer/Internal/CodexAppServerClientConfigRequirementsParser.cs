using System.Linq;
using System.Text.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientConfigRequirementsParser
{
    public static ConfigRequirements? ParseConfigRequirementsReadRequirements(JsonElement configRequirementsReadResult, bool experimentalApiEnabled)
    {
        if (configRequirementsReadResult.ValueKind != JsonValueKind.Object ||
            !configRequirementsReadResult.TryGetProperty("requirements", out var req) ||
            req.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        List<CodexApprovalPolicy>? allowedApprovalPolicyValues = null;
        List<CodexAskForApproval>? allowedAskForApprovalValues = null;
        if (CodexAppServerClientJson.TryGetArray(req, "allowedApprovalPolicies") is { } approvalPoliciesArray)
        {
            foreach (var policyElement in approvalPoliciesArray.EnumerateArray())
            {
                if (CodexAskForApproval.TryParse(policyElement, out var askForApproval))
                {
                    (allowedAskForApprovalValues ??= new List<CodexAskForApproval>()).Add(askForApproval);
                    if (askForApproval.Policy is { } approvalPolicy)
                    {
                        (allowedApprovalPolicyValues ??= new List<CodexApprovalPolicy>()).Add(approvalPolicy);
                    }

                    continue;
                }

                if (policyElement.ValueKind == JsonValueKind.String &&
                    CodexApprovalPolicy.TryParse(policyElement.GetString(), out var parsedPolicy))
                {
                    (allowedApprovalPolicyValues ??= new List<CodexApprovalPolicy>()).Add(parsedPolicy);
                }
            }
        }

        var allowedSandboxModes = GetOptionalStringArray(req, "allowedSandboxModes")
            ?.Select(s => CodexSandboxMode.TryParse(s, out var m) ? m : (CodexSandboxMode?)null)
            .Where(m => m.HasValue)
            .Select(m => m!.Value)
            .ToArray();

        var allowedWindowsSandboxImplementations = GetOptionalStringArray(req, "allowedWindowsSandboxImplementations")
            ?.Select(s => WindowsSandboxSetupMode.TryParse(s, out var m) ? m : (WindowsSandboxSetupMode?)null)
            .Where(m => m.HasValue)
            .Select(m => m!.Value)
            .ToArray();

        var allowedApprovalsReviewers = GetOptionalStringArray(req, "allowedApprovalsReviewers")
            ?.Select(CodexApprovalsReviewerParser.ParseOrNull)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToArray();

        var allowedWebSearchModes = GetOptionalStringArray(req, "allowedWebSearchModes")
            ?.Select(s => CodexWebSearchMode.TryParse(s, out var w) ? w : (CodexWebSearchMode?)null)
            .Where(w => w.HasValue)
            .Select(w => w!.Value)
            .ToArray();

        var featureRequirements = ParseBoolMap(req, "featureRequirements");
        var allowedPermissionProfiles = ParseBoolMap(req, "allowedPermissionProfiles");
        var allowedPermissionProfileIds = allowedPermissionProfiles is null
            ? GetOptionalStringArray(req, "allowedPermissions")
            : allowedPermissionProfiles.Keys.ToArray();

        CodexResidencyRequirement? residency = null;
        if (CodexResidencyRequirement.TryParse(GetStringOrNull(req, "enforceResidency"), out var r))
        {
            residency = r;
        }

        CliAuthCredentialsStoreMode? cliAuthCredentialsStore = null;
        if (CliAuthCredentialsStoreMode.TryParse(GetStringOrNull(req, "cliAuthCredentialsStore"), out var storeMode))
        {
            cliAuthCredentialsStore = storeMode;
        }

        NetworkRequirements? network = null;
        if (experimentalApiEnabled && TryGetObject(req, "network") is { } net)
        {
            network = ParseNetworkRequirements(net);
        }

        return new ConfigRequirements
        {
            AllowedApprovalPolicies = allowedApprovalPolicyValues?.ToArray(),
            AllowedAskForApproval = allowedAskForApprovalValues?.ToArray(),
            AllowedApprovalsReviewers = allowedApprovalsReviewers,
            AllowedSandboxModes = allowedSandboxModes,
            AllowedWindowsSandboxImplementations = allowedWindowsSandboxImplementations,
            AllowedPermissionProfiles = allowedPermissionProfiles,
            AllowedPermissionProfileIds = allowedPermissionProfileIds,
            DefaultPermissionProfileId = GetStringOrNull(req, "defaultPermissions"),
            AllowedWebSearchModes = allowedWebSearchModes,
            FeatureRequirements = featureRequirements,
            AdditionalDeveloperInstructions = GetStringOrNull(req, "additionalDeveloperInstructions"),
            AllowManagedHooksOnly = GetBoolOrNull(req, "allowManagedHooksOnly"),
            AllowBrowserAndComputerUse = GetBoolOrNull(req, "allowBrowserAndComputerUse"),
            AllowAppshots = GetBoolOrNull(req, "allowAppshots"),
            AllowRemoteControl = GetBoolOrNull(req, "allowRemoteControl"),
            AllowLoginShell = GetBoolOrNull(req, "allowLoginShell"),
            CliAuthCredentialsStore = cliAuthCredentialsStore,
            ChatGptBaseUrl = GetStringOrNull(req, "chatgptBaseUrl"),
            CheckForUpdateOnStartup = GetBoolOrNull(req, "checkForUpdateOnStartup"),
            WindowsSandboxPrivateDesktop = GetBoolOrNull(req, "windowsSandboxPrivateDesktop"),
            ComputerUse = TryGetObject(req, "computerUse") is { } computerUse
                ? ParseComputerUseRequirements(computerUse)
                : null,
            BrowserUse = TryGetObject(req, "browserUse") is { } browserUse
                ? ParseBrowserUseRequirements(browserUse)
                : null,
            InAppBrowser = TryGetObject(req, "inAppBrowser") is { } inAppBrowser
                ? new InAppBrowserRequirements
                {
                    AllowExternalBrowserSettingsImport = GetBoolOrNull(inAppBrowser, "allowExternalBrowserSettingsImport"),
                    Raw = inAppBrowser.Clone()
                }
                : null,
            Feedback = TryGetObject(req, "feedback") is { } feedback
                ? new FeedbackRequirements
                {
                    Enabled = GetBoolOrNull(feedback, "enabled"),
                    Raw = feedback.Clone()
                }
                : null,
            SqliteHome = GetStringOrNull(req, "sqliteHome"),
            LogDir = GetStringOrNull(req, "logDir"),
            ModelCatalogJson = GetStringOrNull(req, "modelCatalogJson"),
            Hooks = TryGetObject(req, "hooks")?.Clone(),
            EnforceResidency = residency,
            Network = network,
            AutoReview = TryGetObject(req, "autoReview") is { } autoReview
                ? new AutoReviewRequirements
                {
                    RequiredOnModels = GetOptionalStringArray(autoReview, "requiredOnModels"),
                    IgnoreRules = GetOptionalStringArray(autoReview, "ignoreRules"),
                    Raw = autoReview.Clone()
                }
                : null,
            Raw = req.Clone()
        };
    }

    private static ComputerUseRequirements ParseComputerUseRequirements(JsonElement computerUse)
    {
        return new ComputerUseRequirements
        {
            AllowLockedComputerUse = GetBoolOrNull(computerUse, "allowLockedComputerUse"),
            AllowPersistentApproval = GetBoolOrNull(computerUse, "allowPersistentApproval"),
            DefaultAppAccess = ParseAllowDenyRequirement(GetStringOrNull(computerUse, "defaultAppAccess")),
            Macos = TryGetObject(computerUse, "macos") is { } macos
                ? new ComputerUseMacosRequirements
                {
                    BundleIds = ParseAllowDenyMap(macos, "bundleIds"),
                    Raw = macos.Clone()
                }
                : null,
            Windows = TryGetObject(computerUse, "windows") is { } windows
                ? new ComputerUseWindowsRequirements
                {
                    Aumids = ParseAllowDenyMap(windows, "aumids"),
                    Exes = ParseWindowsExeRequirements(windows),
                    Raw = windows.Clone()
                }
                : null,
            Raw = computerUse.Clone()
        };
    }

    private static BrowserUseRequirements ParseBrowserUseRequirements(JsonElement browserUse)
    {
        return new BrowserUseRequirements
        {
            AllowHistoryAccess = GetBoolOrNull(browserUse, "allowHistoryAccess"),
            DisableAutoReview = GetBoolOrNull(browserUse, "disableAutoReview"),
            AllowGlobalPersistentApproval = GetBoolOrNull(browserUse, "allowGlobalPersistentApproval"),
            DefaultOriginPolicy = TryGetObject(browserUse, "defaultOriginPolicy") is { } defaultPolicy
                ? ParseBrowserUseOriginPolicy(defaultPolicy)
                : null,
            Origins = ParseBrowserUseOriginPolicyMap(browserUse, "origins"),
            Raw = browserUse.Clone()
        };
    }

    private static BrowserUseOriginPolicy ParseBrowserUseOriginPolicy(JsonElement policy)
    {
        return new BrowserUseOriginPolicy
        {
            Access = ParseAllowDenyRequirement(GetStringOrNull(policy, "access")),
            Downloads = ParseAllowDenyRequirement(GetStringOrNull(policy, "downloads")),
            Uploads = ParseAllowDenyRequirement(GetStringOrNull(policy, "uploads")),
            FullCdpAccess = ParseAllowDenyRequirement(GetStringOrNull(policy, "fullCdpAccess")),
            AutoReview = ParseAllowDenyRequirement(GetStringOrNull(policy, "autoReview")),
            PersistentApproval = GetBoolOrNull(policy, "persistentApproval"),
            AccessApprovalLifetime = ParseBrowserUseAccessApprovalLifetime(GetStringOrNull(policy, "accessApprovalLifetime")),
            Raw = policy.Clone()
        };
    }

    private static AllowDenyRequirementValue? ParseAllowDenyRequirement(string? value) =>
        AllowDenyRequirementValue.TryParse(value, out var requirement) ? requirement : (AllowDenyRequirementValue?)null;

    private static BrowserUseAccessApprovalLifetimeValue? ParseBrowserUseAccessApprovalLifetime(string? value) =>
        BrowserUseAccessApprovalLifetimeValue.TryParse(value, out var lifetime) ? lifetime : (BrowserUseAccessApprovalLifetimeValue?)null;

    private static NetworkRequirements ParseNetworkRequirements(JsonElement network)
    {
        return new NetworkRequirements
        {
            Enabled = GetBoolOrNull(network, "enabled"),
            HttpPort = GetInt32OrNull(network, "httpPort"),
            SocksPort = GetInt32OrNull(network, "socksPort"),
            AllowUpstreamProxy = GetBoolOrNull(network, "allowUpstreamProxy"),
            DangerouslyAllowNonLoopbackProxy = GetBoolOrNull(network, "dangerouslyAllowNonLoopbackProxy"),
            DangerouslyAllowAllUnixSockets = GetBoolOrNull(network, "dangerouslyAllowAllUnixSockets"),
            Domains = ParseDomainPermissions(network, "domains"),
            ManagedAllowedDomainsOnly = GetBoolOrNull(network, "managedAllowedDomainsOnly"),
            AllowedDomains = GetOptionalStringArray(network, "allowedDomains"),
            DeniedDomains = GetOptionalStringArray(network, "deniedDomains"),
            AllowUnixSockets = GetOptionalStringArray(network, "allowUnixSockets"),
            UnixSockets = ParseUnixSocketPermissions(network, "unixSockets"),
            AllowLocalBinding = GetBoolOrNull(network, "allowLocalBinding"),
            Raw = network.Clone()
        };
    }

    private static IReadOnlyDictionary<string, bool>? ParseBoolMap(JsonElement obj, string propertyName)
    {
        if (TryGetObject(obj, propertyName) is not { } values)
        {
            return null;
        }

        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var item in values.EnumerateObject())
        {
            if (item.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                result[item.Name] = item.Value.GetBoolean();
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyDictionary<string, AllowDenyRequirementValue>? ParseAllowDenyMap(JsonElement obj, string propertyName)
    {
        if (TryGetObject(obj, propertyName) is not { } values)
        {
            return null;
        }

        var result = new Dictionary<string, AllowDenyRequirementValue>(StringComparer.Ordinal);
        foreach (var item in values.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (AllowDenyRequirementValue.TryParse(item.Value.GetString(), out var requirement))
            {
                result[item.Name] = requirement;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyDictionary<string, BrowserUseOriginPolicy>? ParseBrowserUseOriginPolicyMap(JsonElement obj, string propertyName)
    {
        if (TryGetObject(obj, propertyName) is not { } values)
        {
            return null;
        }

        var result = new Dictionary<string, BrowserUseOriginPolicy>(StringComparer.Ordinal);
        foreach (var item in values.EnumerateObject())
        {
            if (item.Value.ValueKind == JsonValueKind.Object)
            {
                result[item.Name] = ParseBrowserUseOriginPolicy(item.Value);
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyList<ComputerUseWindowsExeRequirement>? ParseWindowsExeRequirements(JsonElement windows)
    {
        if (TryGetArray(windows, "exes") is not { } exes)
        {
            return null;
        }

        var result = new List<ComputerUseWindowsExeRequirement>();
        foreach (var exe in exes.EnumerateArray())
        {
            if (exe.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var publisherName = GetStringOrNull(exe, "publisherName");
            var productName = GetStringOrNull(exe, "productName");
            if (string.IsNullOrWhiteSpace(publisherName) ||
                string.IsNullOrWhiteSpace(productName) ||
                !AllowDenyRequirementValue.TryParse(GetStringOrNull(exe, "access"), out var access))
            {
                continue;
            }

            result.Add(new ComputerUseWindowsExeRequirement
            {
                PublisherName = publisherName,
                ProductName = productName,
                BinaryName = GetStringOrNull(exe, "binaryName"),
                Access = access,
                Raw = exe.Clone()
            });
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyDictionary<string, NetworkDomainPermission>? ParseDomainPermissions(JsonElement obj, string propertyName)
    {
        if (TryGetObject(obj, propertyName) is not { } permissions)
        {
            return null;
        }

        var result = new Dictionary<string, NetworkDomainPermission>(StringComparer.Ordinal);
        foreach (var item in permissions.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (NetworkDomainPermission.TryParse(item.Value.GetString(), out var permission))
            {
                result[item.Name] = permission;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyDictionary<string, NetworkUnixSocketPermission>? ParseUnixSocketPermissions(JsonElement obj, string propertyName)
    {
        if (TryGetObject(obj, propertyName) is not { } permissions)
        {
            return null;
        }

        var result = new Dictionary<string, NetworkUnixSocketPermission>(StringComparer.Ordinal);
        foreach (var item in permissions.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (NetworkUnixSocketPermission.TryParse(item.Value.GetString(), out var permission))
            {
                result[item.Name] = permission;
            }
        }

        return result.Count == 0 ? null : result;
    }
}
