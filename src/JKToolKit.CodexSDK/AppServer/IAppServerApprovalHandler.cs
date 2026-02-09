using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Handles app-server approval requests (for example, tool execution approvals).
/// </summary>
public interface IAppServerApprovalHandler
{
    /// <summary>
    /// Handles an approval request from the app server and returns a JSON response payload.
    /// </summary>
    /// <param name="method">The request method name.</param>
    /// <param name="params">Optional request parameters payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A JSON element representing the approval response payload.</returns>
    ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct);
}

