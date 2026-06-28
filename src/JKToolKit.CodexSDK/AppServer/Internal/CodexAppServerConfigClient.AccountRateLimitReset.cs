using System.Text.Json;
using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerConfigClient
{
    public async Task<AccountRateLimitResetCreditConsumeResult> ConsumeAccountRateLimitResetCreditAsync(
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey cannot be empty or whitespace.", nameof(idempotencyKey));

        var result = await _sendRequestAsync(
            "account/rateLimitResetCredit/consume",
            new UpstreamV2.ConsumeAccountRateLimitResetCreditParams
            {
                IdempotencyKey = idempotencyKey
            },
            ct);

        var outcome = CodexAppServerClientJson.GetRequiredString(
            result,
            "outcome",
            "account/rateLimitResetCredit/consume response");

        return new AccountRateLimitResetCreditConsumeResult
        {
            Outcome = outcome,
            OutcomeKind = ParseRateLimitResetCreditConsumeOutcome(outcome),
            Raw = result
        };
    }

    private static RateLimitResetCreditsSummary? ParseRateLimitResetCredits(JsonElement result)
    {
        var credits = CodexAppServerClientJson.TryGetObject(result, "rateLimitResetCredits");
        if (credits is null)
        {
            return null;
        }

        return new RateLimitResetCreditsSummary
        {
            AvailableCount = CodexAppServerClientJson.GetRequiredInt64(
                credits.Value,
                "availableCount",
                "account/rateLimits/read rateLimitResetCredits"),
            Raw = credits.Value.Clone()
        };
    }

    private static AccountRateLimitResetCreditConsumeOutcome ParseRateLimitResetCreditConsumeOutcome(string value) =>
        value switch
        {
            "reset" => AccountRateLimitResetCreditConsumeOutcome.Reset,
            "nothingToReset" => AccountRateLimitResetCreditConsumeOutcome.NothingToReset,
            "noCredit" => AccountRateLimitResetCreditConsumeOutcome.NoCredit,
            "alreadyRedeemed" => AccountRateLimitResetCreditConsumeOutcome.AlreadyRedeemed,
            _ => AccountRateLimitResetCreditConsumeOutcome.Unknown
        };
}
