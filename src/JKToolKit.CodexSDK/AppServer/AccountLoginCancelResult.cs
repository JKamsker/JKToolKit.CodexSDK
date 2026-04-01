using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Result returned by <c>account/login/cancel</c>.
/// </summary>
public sealed record class AccountLoginCancelResult
{
    /// <summary>
    /// Gets the cancellation status.
    /// </summary>
    public AccountLoginCancelStatus Status { get; init; }

    /// <summary>
    /// Gets the raw JSON payload returned by the app-server.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
