using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Handles server-initiated requests sent by the app-server.
/// </summary>
/// <remarks>
/// This handler is invoked for server request methods including (but not limited to):
/// <c>item/commandExecution/requestApproval</c>, <c>item/fileChange/requestApproval</c>,
/// <c>execCommandApproval</c>, <c>applyPatchApproval</c>, <c>item/tool/requestUserInput</c>,
/// <c>item/tool/call</c>, and <c>account/chatgptAuthTokens/refresh</c>.
/// </remarks>
public interface IAppServerApprovalHandler
{
    /// <summary>
    /// Handles a server request from the app server and returns a JSON response payload.
    /// </summary>
    /// <param name="method">The request method name.</param>
    /// <param name="params">Optional request parameters payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A JSON element representing the response payload.</returns>
    ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct);
}

