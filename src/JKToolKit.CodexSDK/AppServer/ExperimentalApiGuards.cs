namespace JKToolKit.CodexSDK.AppServer;

internal static class ExperimentalApiGuards
{
    internal static void ValidateThreadStart(ThreadStartOptions options, bool experimentalApiEnabled)
    {
        if (!options.ExperimentalRawEvents)
        {
            return;
        }

        if (!experimentalApiEnabled)
        {
            throw new CodexExperimentalApiRequiredException("thread/start.experimentalRawEvents");
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
        }
    }

    internal static void ValidateThreadFork(ThreadForkOptions options, bool experimentalApiEnabled)
    {
        if (string.IsNullOrWhiteSpace(options.ThreadId) && string.IsNullOrWhiteSpace(options.Path))
        {
            throw new ArgumentException("Either ThreadId or Path must be specified.", nameof(options));
        }

        if (!experimentalApiEnabled && !string.IsNullOrWhiteSpace(options.Path))
        {
            throw new CodexExperimentalApiRequiredException("thread/fork.path");
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
