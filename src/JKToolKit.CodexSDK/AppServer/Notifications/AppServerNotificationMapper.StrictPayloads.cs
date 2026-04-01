using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationMapper
{
    private static AccountUpdatedNotification? TryMapAccountUpdated(JsonElement p)
    {
        try
        {
            return new AccountUpdatedNotification(
                AuthMode: CodexAppServerAccountParsers.ParseAuthModeOrNull(p, "authMode", "account/updated"),
                PlanType: CodexAppServerAccountParsers.ParsePlanTypeOrNull(p, "planType", "account/updated"),
                Params: p);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static FsChangedNotification? TryMapFsChanged(JsonElement p)
    {
        var watchId = GetString(p, "watchId");
        if (string.IsNullOrWhiteSpace(watchId) ||
            !p.TryGetProperty("changedPaths", out var changedPathsElement) ||
            changedPathsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var changedPaths = new List<string>();
        foreach (var item in changedPathsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            try
            {
                changedPaths.Add(CodexAppServerPathValidation.RequireAbsolutePayloadPath(
                    item.GetString(),
                    "changedPaths",
                    "fs/changed"));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        return new FsChangedNotification(
            WatchId: watchId,
            ChangedPaths: changedPaths,
            Params: p);
    }

    private static McpServerStartupStatusUpdatedNotification? TryMapMcpServerStartupStatusUpdated(JsonElement p)
    {
        var name = GetString(p, "name");
        var status = GetString(p, "status");
        if (string.IsNullOrWhiteSpace(name) ||
            !TryParseMcpServerStartupState(status, out var parsedStatus))
        {
            return null;
        }

        return new McpServerStartupStatusUpdatedNotification(
            Name: name,
            Status: parsedStatus,
            Error: GetStringOrNull(p, "error"),
            Params: p);
    }

    private static bool TryParseMcpServerStartupState(string? value, out McpServerStartupState status)
    {
        switch (value)
        {
            case "starting":
                status = McpServerStartupState.Starting;
                return true;
            case "ready":
                status = McpServerStartupState.Ready;
                return true;
            case "failed":
                status = McpServerStartupState.Failed;
                return true;
            case "cancelled":
                status = McpServerStartupState.Cancelled;
                return true;
            default:
                status = default;
                return false;
        }
    }
}
