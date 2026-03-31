using JKToolKit.CodexSDK.Abstractions;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal static class CodexResumeTargetResolver
{
    internal static async Task<CodexSessionInfo?> TryResolveAsync(
        ICodexSessionLocator sessionLocator,
        string sessionsRoot,
        CodexResumeTarget target,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionLocator);
        ArgumentNullException.ThrowIfNull(sessionsRoot);
        ArgumentNullException.ThrowIfNull(target);

        target.Validate();

        var filter = BuildFilter(target, workingDirectory);
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
                string.Equals(session.HumanLabel, target.Selector, StringComparison.OrdinalIgnoreCase))
            {
                humanLabelMatch = session;
            }
        }

        return humanLabelMatch;
    }

    private static SessionFilter? BuildFilter(CodexResumeTarget target, string? workingDirectory)
    {
        if (target.IncludeAllSessions || string.IsNullOrWhiteSpace(workingDirectory))
        {
            return null;
        }

        return new SessionFilter(WorkingDirectory: workingDirectory);
    }
}
