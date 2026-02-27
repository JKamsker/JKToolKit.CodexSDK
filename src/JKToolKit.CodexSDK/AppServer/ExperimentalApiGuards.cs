namespace JKToolKit.CodexSDK.AppServer;

internal static class ExperimentalApiGuards
{
    internal static void ValidateThreadStart(ThreadStartOptions options, bool experimentalApiEnabled)
    {
        if (!experimentalApiEnabled)
        {
            if (options.ExperimentalRawEvents)
            {
                throw new CodexExperimentalApiRequiredException("thread/start.experimentalRawEvents");
            }

            if (options.DynamicTools is { Count: > 0 })
            {
                throw new CodexExperimentalApiRequiredException("thread/start.dynamicTools");
            }

            if (options.PersistExtendedHistory)
            {
                throw new CodexExperimentalApiRequiredException("thread/start.persistFullHistory");
            }
        }
    }

    internal static void ValidateThreadResume(ThreadResumeOptions options, bool experimentalApiEnabled)
    {
        if (!experimentalApiEnabled)
        {
            if (options.History is not null)
            {
                throw new CodexExperimentalApiRequiredException("thread/resume.history");
            }

            if (!string.IsNullOrWhiteSpace(options.Path))
            {
                throw new CodexExperimentalApiRequiredException("thread/resume.path");
            }

            if (options.PersistExtendedHistory)
            {
                throw new CodexExperimentalApiRequiredException("thread/resume.persistFullHistory");
            }
        }
    }

    internal static void ValidateThreadFork(ThreadForkOptions options, bool experimentalApiEnabled)
    {
        var hasThreadId = !string.IsNullOrWhiteSpace(options.ThreadId);
        var hasPath = !string.IsNullOrWhiteSpace(options.Path);

        if (!hasThreadId && !hasPath)
        {
            throw new ArgumentException("Either ThreadId or Path must be specified.", nameof(options));
        }

        if (hasThreadId && hasPath)
        {
            throw new ArgumentException("Specify either ThreadId or Path, not both.", nameof(options));
        }

        if (!experimentalApiEnabled && hasPath)
        {
            throw new CodexExperimentalApiRequiredException("thread/fork.path");
        }

        if (!experimentalApiEnabled && options.PersistExtendedHistory)
        {
            throw new CodexExperimentalApiRequiredException("thread/fork.persistFullHistory");
        }
    }

    internal static void ValidateTurnStart(TurnStartOptions options, bool experimentalApiEnabled)
    {
        if (!experimentalApiEnabled && options.CollaborationMode is not null)
        {
            throw new CodexExperimentalApiRequiredException("turn/start.collaborationMode");
        }
    }
}
