using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
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

    internal static IReadOnlyList<TurnEnvironmentParams>? BuildEnvironments(
        IReadOnlyList<TurnEnvironmentOptions>? environments,
        string argumentName)
    {
        if (environments is null)
        {
            return null;
        }

        var result = new TurnEnvironmentParams[environments.Count];
        for (var i = 0; i < environments.Count; i++)
        {
            var environment = environments[i] ?? throw new ArgumentException(
                "Environment entries cannot be null.",
                argumentName);

            if (string.IsNullOrWhiteSpace(environment.EnvironmentId))
            {
                throw new ArgumentException("EnvironmentId cannot be empty or whitespace.", argumentName);
            }

            CodexAppServerPathValidation.ValidateRequiredAbsolutePath(environment.Cwd, argumentName, "Environment Cwd");

            result[i] = new TurnEnvironmentParams
            {
                EnvironmentId = environment.EnvironmentId,
                Cwd = environment.Cwd
            };
        }

        return result;
    }
}
