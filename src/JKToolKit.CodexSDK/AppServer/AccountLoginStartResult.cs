using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Result returned by <c>account/login/start</c>.
/// </summary>
public abstract record class AccountLoginStartResult
{
    /// <summary>
    /// Gets the raw JSON payload returned by the app-server.
    /// </summary>
    public required JsonElement Raw { get; init; }

    /// <summary>
    /// Result for API-key login.
    /// </summary>
    public sealed record class ApiKey : AccountLoginStartResult;

    /// <summary>
    /// Result for browser-based ChatGPT login.
    /// </summary>
    public sealed record class ChatGptBrowser : AccountLoginStartResult
    {
        /// <summary>
        /// Gets the server-generated login identifier.
        /// </summary>
        public required string LoginId { get; init; }

        /// <summary>
        /// Gets the browser URL to open for authorization.
        /// </summary>
        public required string AuthUrl { get; init; }
    }

    /// <summary>
    /// Result for ChatGPT device-code login.
    /// </summary>
    public sealed record class ChatGptDeviceCode : AccountLoginStartResult
    {
        /// <summary>
        /// Gets the server-generated login identifier.
        /// </summary>
        public required string LoginId { get; init; }

        /// <summary>
        /// Gets the verification URL the user should open.
        /// </summary>
        public required string VerificationUrl { get; init; }

        /// <summary>
        /// Gets the user code the user should enter.
        /// </summary>
        public required string UserCode { get; init; }
    }

    /// <summary>
    /// Result for externally managed ChatGPT auth-token login.
    /// </summary>
    public sealed record class ChatGptAuthTokens : AccountLoginStartResult;
}
