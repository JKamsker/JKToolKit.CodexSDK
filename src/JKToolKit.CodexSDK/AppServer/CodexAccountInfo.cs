using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents typed account information returned by <c>account/read</c>.
/// </summary>
public abstract record class CodexAccountInfo(JsonElement Raw)
{
    /// <summary>
    /// Gets the upstream account type discriminator.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// Gets the raw JSON payload for the account.
    /// </summary>
    public JsonElement Raw { get; } = Raw;
}

/// <summary>
/// Represents API-key backed account state.
/// </summary>
public sealed record class CodexApiKeyAccountInfo(JsonElement Raw) : CodexAccountInfo(Raw)
{
    /// <inheritdoc />
    public override string Type => "apiKey";
}

/// <summary>
/// Represents managed ChatGPT account state.
/// </summary>
public sealed record class CodexChatGptAccountInfo(string Email, CodexPlanType PlanType, JsonElement Raw) : CodexAccountInfo(Raw)
{
    /// <inheritdoc />
    public override string Type => "chatgpt";
}
