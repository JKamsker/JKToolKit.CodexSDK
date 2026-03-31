namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for starting an account login flow via the Codex app-server.
/// </summary>
public abstract record class AccountLoginStartOptions
{
    /// <summary>
    /// Starts API-key login.
    /// </summary>
    public sealed record class ApiKey : AccountLoginStartOptions
    {
        /// <summary>
        /// Initializes a new API-key login request.
        /// </summary>
        public ApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be empty or whitespace.", nameof(apiKey));

            ApiKeyValue = apiKey;
        }

        /// <summary>
        /// Gets the API key to persist.
        /// </summary>
        public string ApiKeyValue { get; }
    }

    /// <summary>
    /// Starts a browser-based managed ChatGPT login flow.
    /// </summary>
    public sealed record class ChatGptBrowser : AccountLoginStartOptions;

    /// <summary>
    /// Starts a managed ChatGPT device-code login flow.
    /// </summary>
    public sealed record class ChatGptDeviceCode : AccountLoginStartOptions;

    /// <summary>
    /// Supplies externally managed ChatGPT auth tokens.
    /// </summary>
    public sealed record class ChatGptAuthTokens : AccountLoginStartOptions
    {
        /// <summary>
        /// Initializes a new externally managed ChatGPT token login request.
        /// </summary>
        public ChatGptAuthTokens(string accessToken, string chatGptAccountId, string? chatGptPlanType = null)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Access token cannot be empty or whitespace.", nameof(accessToken));

            if (string.IsNullOrWhiteSpace(chatGptAccountId))
                throw new ArgumentException("ChatGPT account id cannot be empty or whitespace.", nameof(chatGptAccountId));

            AccessToken = accessToken;
            ChatGptAccountId = chatGptAccountId;
            ChatGptPlanType = chatGptPlanType;
        }

        /// <summary>
        /// Gets the externally managed ChatGPT access token.
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// Gets the externally managed ChatGPT account identifier.
        /// </summary>
        public string ChatGptAccountId { get; }

        /// <summary>
        /// Gets the optional externally managed ChatGPT plan type.
        /// </summary>
        public string? ChatGptPlanType { get; }
    }
}
