using JKToolKit.CodexSDK.Abstractions;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal static class CodexResumeTargetResolver
{
    internal static async Task<CodexSessionInfo?> TryResolveAsync(
        ICodexSessionLocator sessionLocator,
        string sessionsRoot,
        CodexResumeTarget target,
        string? workingDirectory,
        string? modelProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionLocator);
        ArgumentNullException.ThrowIfNull(sessionsRoot);
        ArgumentNullException.ThrowIfNull(target);

        target.Validate();

        var filter = BuildFilter(target, workingDirectory, modelProvider);
        if (target.UseMostRecent)
        {
            await foreach (var session in sessionLocator.ListSessionsAsync(sessionsRoot, filter, cancellationToken).ConfigureAwait(false))
            {
                return session;
            }

            return null;
        }

        CodexSessionInfo? humanLabelMatch = null;
        await foreach (var session in sessionLocator.ListSessionsAsync(sessionsRoot, filter, cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(session.Id.Value, target.Selector, StringComparison.OrdinalIgnoreCase))
            {
                return session;
            }

            if (humanLabelMatch is null &&
                !string.IsNullOrWhiteSpace(session.HumanLabel) &&
                string.Equals(session.HumanLabel, target.Selector, StringComparison.Ordinal))
            {
                humanLabelMatch = session;
            }
        }

        return humanLabelMatch;
    }

    private static SessionFilter? BuildFilter(CodexResumeTarget target, string? workingDirectory, string? modelProvider)
    {
        var effectiveWorkingDirectory = target.IncludeAllSessions ? null : workingDirectory;
        var effectiveModelProvider = target.UseMostRecent ? modelProvider : null;

        if (string.IsNullOrWhiteSpace(effectiveWorkingDirectory) &&
            string.IsNullOrWhiteSpace(effectiveModelProvider))
        {
            return null;
        }

        return new SessionFilter(
            WorkingDirectory: effectiveWorkingDirectory,
            ModelProvider: effectiveModelProvider);
    }
}
