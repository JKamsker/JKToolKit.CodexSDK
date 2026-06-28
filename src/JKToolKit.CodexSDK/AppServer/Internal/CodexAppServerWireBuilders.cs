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

    internal static IReadOnlyList<string>? BuildRuntimeWorkspaceRoots(
        IReadOnlyList<string>? runtimeWorkspaceRoots,
        string argumentName)
    {
        CodexAppServerPathValidation.ValidateOptionalAbsolutePaths(
            runtimeWorkspaceRoots,
            argumentName,
            "RuntimeWorkspaceRoots");

        return runtimeWorkspaceRoots;
    }

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

            result[i] = new TurnEnvironmentParams
            {
                EnvironmentId = environment.EnvironmentId,
                Cwd = environment.Cwd
            };
        }

        return result;
    }

    internal static IReadOnlyDictionary<string, TurnAdditionalContextEntryParams>? BuildAdditionalContext(
        IReadOnlyDictionary<string, TurnAdditionalContextEntry>? additionalContext,
        string argumentName)
    {
        if (additionalContext is null)
        {
            return null;
        }

        var result = new Dictionary<string, TurnAdditionalContextEntryParams>(StringComparer.Ordinal);
        foreach (var entry in additionalContext)
        {
            if (entry.Value is null)
            {
                throw new ArgumentException("Additional context entries cannot be null.", argumentName);
            }

            result[entry.Key] = new TurnAdditionalContextEntryParams
            {
                Value = entry.Value.Value,
                Kind = entry.Value.Kind.Value
            };
        }

        return result;
    }

    internal static ThreadResumeInitialTurnsPageParams? BuildInitialTurnsPage(
        ThreadResumeInitialTurnsPageOptions? options,
        string argumentName)
    {
        if (options is null)
        {
            return null;
        }

        if (options.Limit is < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, options.Limit, "Initial turn page limit cannot be negative.");
        }

        return new ThreadResumeInitialTurnsPageParams
        {
            Limit = options.Limit,
            SortDirection = options.SortDirection,
            ItemsView = options.ItemsView
        };
    }
}
