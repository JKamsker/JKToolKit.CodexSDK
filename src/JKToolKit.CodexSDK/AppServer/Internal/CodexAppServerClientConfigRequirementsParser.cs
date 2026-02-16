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

        var allowedApprovalPolicies = GetOptionalStringArray(req, "allowedApprovalPolicies")
            ?.Select(CodexApprovalPolicy.Parse)
            .ToArray();

        var allowedSandboxModes = GetOptionalStringArray(req, "allowedSandboxModes")
            ?.Select(CodexSandboxMode.Parse)
            .ToArray();

        var allowedWebSearchModes = GetOptionalStringArray(req, "allowedWebSearchModes")
            ?.Select(CodexWebSearchMode.Parse)
            .ToArray();

        CodexResidencyRequirement? residency = null;
        if (CodexResidencyRequirement.TryParse(GetStringOrNull(req, "enforceResidency"), out var r))
        {
            residency = r;
        }

        NetworkRequirements? network = null;
        if (experimentalApiEnabled && TryGetObject(req, "network") is { } net)
        {
            network = ParseNetworkRequirements(net);
        }

        return new ConfigRequirements
        {
            AllowedApprovalPolicies = allowedApprovalPolicies,
            AllowedSandboxModes = allowedSandboxModes,
            AllowedWebSearchModes = allowedWebSearchModes,
            EnforceResidency = residency,
            Network = network,
            Raw = req.Clone()
        };
    }

    private static NetworkRequirements ParseNetworkRequirements(JsonElement network)
    {
        return new NetworkRequirements
        {
            Enabled = GetBoolOrNull(network, "enabled"),
            HttpPort = GetInt32OrNull(network, "httpPort"),
            SocksPort = GetInt32OrNull(network, "socksPort"),
            AllowUpstreamProxy = GetBoolOrNull(network, "allowUpstreamProxy"),
            DangerouslyAllowNonLoopbackProxy = GetBoolOrNull(network, "dangerouslyAllowNonLoopbackProxy"),
            DangerouslyAllowNonLoopbackAdmin = GetBoolOrNull(network, "dangerouslyAllowNonLoopbackAdmin"),
            AllowedDomains = GetOptionalStringArray(network, "allowedDomains"),
            DeniedDomains = GetOptionalStringArray(network, "deniedDomains"),
            AllowUnixSockets = GetOptionalStringArray(network, "allowUnixSockets"),
            AllowLocalBinding = GetBoolOrNull(network, "allowLocalBinding"),
            Raw = network.Clone()
        };
    }
}
