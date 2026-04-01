using System.Text.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerWireBuilders
{
    internal static JsonElement? BuildServiceTier(CodexServiceTier? serviceTier, bool clearServiceTier, string argumentName)
    {
        if (clearServiceTier)
        {
            if (serviceTier is not null)
            {
                throw new ArgumentException(
                    "ServiceTier and ClearServiceTier cannot both be set.",
                    argumentName);
            }

            return JsonSerializer.SerializeToElement<string?>(null);
        }

        return serviceTier is { } tier
            ? JsonSerializer.SerializeToElement(tier.Value)
            : null;
    }

    internal static string BuildThreadIdOrPlaceholder(string? threadId) =>
        string.IsNullOrWhiteSpace(threadId) ? string.Empty : threadId;
}
