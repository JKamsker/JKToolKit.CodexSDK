using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.AppServer.Overrides;

/// <summary>
/// Maps app-server JSON-RPC notifications to typed notification objects.
/// </summary>
/// <remarks>
/// This interface enables consumers to override SDK notification mapping for specific methods
/// to stay compatible with upstream changes without forking the SDK.
/// </remarks>
public interface IAppServerNotificationMapper
{
    /// <summary>
    /// Attempts to map a notification to a typed instance.
    /// </summary>
    /// <param name="method">The JSON-RPC method name.</param>
    /// <param name="params">The raw JSON-RPC params payload (never null).</param>
    /// <returns>A mapped notification instance, or null if this mapper does not handle the method.</returns>
    AppServerNotification? TryMap(string method, JsonElement @params);
}

