using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Overrides;

/// <summary>
/// Observes app-server JSON-RPC traffic (best-effort) for diagnostics/telemetry.
/// </summary>
/// <remarks>
/// Implementations must be fast and should not throw. Exceptions are swallowed by the SDK.
/// </remarks>
public interface IAppServerMessageObserver
{
    /// <summary>
    /// Called before an outbound request is sent.
    /// </summary>
    void OnRequest(string method, JsonElement @params);

    /// <summary>
    /// Called after a response result has been received.
    /// </summary>
    void OnResponse(string method, JsonElement result);

    /// <summary>
    /// Called when a JSON-RPC notification has been received.
    /// </summary>
    void OnNotification(string method, JsonElement @params);
}

