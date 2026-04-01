using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

internal static class AppServerApprovalDecisionJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement CreateCommandExecutionResponse(CommandExecutionRequestApprovalParams? request, bool approve) =>
        CreateDecisionResponse(SelectDecision(
            request?.AvailableDecisions,
            approve ? ["accept", "acceptForSession", "acceptWithExecpolicyAmendment", "applyNetworkPolicyAmendment"] : ["decline", "cancel"],
            approve ? "accept" : "decline"));

    public static JsonElement CreateFileChangeResponse(FileChangeRequestApprovalParams? request, bool approve) =>
        CreateDecisionResponse(SelectDecision(
            request?.AvailableDecisions,
            approve ? ["accept", "acceptForSession"] : ["decline", "cancel"],
            approve ? "accept" : "decline"));

    public static JsonElement CreateDecisionResponse(JsonElement decision) =>
        JsonSerializer.SerializeToElement(new { decision }, SerializerOptions);

    public static string DescribeDecision(JsonElement decision)
    {
        if (decision.ValueKind == JsonValueKind.String)
        {
            return decision.GetString() ?? string.Empty;
        }

        if (decision.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in decision.EnumerateObject())
            {
                return property.Name;
            }
        }

        return decision.GetRawText();
    }

    private static JsonElement SelectDecision(IReadOnlyList<JsonElement>? availableDecisions, IReadOnlyList<string> preferredKinds, string fallbackKind)
    {
        if (availableDecisions is { Count: > 0 })
        {
            foreach (var preferredKind in preferredKinds)
            {
                foreach (var availableDecision in availableDecisions)
                {
                    if (DecisionMatches(availableDecision, preferredKind))
                    {
                        return availableDecision.Clone();
                    }
                }
            }

            return availableDecisions[0].Clone();
        }

        using var doc = JsonDocument.Parse($"\"{fallbackKind}\"");
        return doc.RootElement.Clone();
    }

    private static bool DecisionMatches(JsonElement decision, string kind)
    {
        if (decision.ValueKind == JsonValueKind.String)
        {
            return string.Equals(decision.GetString(), kind, StringComparison.Ordinal);
        }

        if (decision.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in decision.EnumerateObject())
            {
                return string.Equals(property.Name, kind, StringComparison.Ordinal);
            }
        }

        return false;
    }
}
