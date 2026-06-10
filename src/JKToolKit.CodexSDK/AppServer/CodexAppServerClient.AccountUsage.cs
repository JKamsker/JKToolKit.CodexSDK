namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Reads ChatGPT account token-usage summary and daily buckets.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>account/usage/read</c>.
    /// </remarks>
    public Task<AccountTokenUsageReadResult> ReadAccountTokenUsageAsync(CancellationToken ct = default) =>
        _configClient.ReadAccountTokenUsageAsync(ct);
}
