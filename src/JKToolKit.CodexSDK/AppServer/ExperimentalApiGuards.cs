namespace JKToolKit.CodexSDK.AppServer;

internal static class ExperimentalApiGuards
{
    internal static void ValidateThreadStart(ThreadStartOptions options, bool experimentalApiEnabled)
    {
        if (options.Sandbox is not null && !string.IsNullOrWhiteSpace(options.PermissionProfileId))
        {
            throw new ArgumentException("Sandbox and PermissionProfileId cannot both be set.", nameof(options));
        }

        if (!experimentalApiEnabled)
        {
            if (options.AskForApproval is { Granular: not null })
            {
                throw new CodexExperimentalApiRequiredException("askForApproval.granular");
            }

            if (options.ExperimentalRawEvents)
            {
                throw new CodexExperimentalApiRequiredException("thread/start.experimentalRawEvents");
            }

            if (options.DynamicTools is not null)
            {
                throw new CodexExperimentalApiRequiredException("thread/start.dynamicTools");
            }

            if (options.PersistExtendedHistory)
            {
                throw new CodexExperimentalApiRequiredException("thread/start.persistFullHistory");
            }

            if (options.RuntimeWorkspaceRoots is not null)
            {
                throw new CodexExperimentalApiRequiredException("thread/start.runtimeWorkspaceRoots");
            }

            if (options.Environments is not null)
            {
                throw new CodexExperimentalApiRequiredException("thread/start.environments");
            }

            if (!string.IsNullOrWhiteSpace(options.PermissionProfileId))
            {
                throw new CodexExperimentalApiRequiredException("thread/start.permissions");
            }
        }
    }

    internal static void ValidateThreadResume(ThreadResumeOptions options, bool experimentalApiEnabled)
    {
        if (options.Sandbox is not null && !string.IsNullOrWhiteSpace(options.PermissionProfileId))
        {
            throw new ArgumentException("Sandbox and PermissionProfileId cannot both be set.", nameof(options));
        }

        if (!experimentalApiEnabled)
        {
            if (options.AskForApproval is { Granular: not null })
            {
                throw new CodexExperimentalApiRequiredException("askForApproval.granular");
            }

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

            if (options.RuntimeWorkspaceRoots is not null)
            {
                throw new CodexExperimentalApiRequiredException("thread/resume.runtimeWorkspaceRoots");
            }

            if (!string.IsNullOrWhiteSpace(options.PermissionProfileId))
            {
                throw new CodexExperimentalApiRequiredException("thread/resume.permissions");
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

        if (options.Sandbox is not null && !string.IsNullOrWhiteSpace(options.PermissionProfileId))
        {
            throw new ArgumentException("Sandbox and PermissionProfileId cannot both be set.", nameof(options));
        }

        if (!experimentalApiEnabled && hasPath)
        {
            throw new CodexExperimentalApiRequiredException("thread/fork.path");
        }

        if (!experimentalApiEnabled && options.AskForApproval is { Granular: not null })
        {
            throw new CodexExperimentalApiRequiredException("askForApproval.granular");
        }

        if (!experimentalApiEnabled && options.PersistExtendedHistory)
        {
            throw new CodexExperimentalApiRequiredException("thread/fork.persistFullHistory");
        }

        if (!experimentalApiEnabled && options.RuntimeWorkspaceRoots is not null)
        {
            throw new CodexExperimentalApiRequiredException("thread/fork.runtimeWorkspaceRoots");
        }

        if (!experimentalApiEnabled && !string.IsNullOrWhiteSpace(options.PermissionProfileId))
        {
            throw new CodexExperimentalApiRequiredException("thread/fork.permissions");
        }
    }

    internal static void ValidateTurnStart(TurnStartOptions options, bool experimentalApiEnabled)
    {
        if (options.SandboxPolicy is not null && !string.IsNullOrWhiteSpace(options.PermissionProfileId))
        {
            throw new ArgumentException("SandboxPolicy and PermissionProfileId cannot both be set.", nameof(options));
        }

        if (!experimentalApiEnabled && options.AskForApproval is { Granular: not null })
        {
            throw new CodexExperimentalApiRequiredException("askForApproval.granular");
        }

        if (!experimentalApiEnabled && options.CollaborationMode is not null)
        {
            throw new CodexExperimentalApiRequiredException("turn/start.collaborationMode");
        }

        if (!experimentalApiEnabled && options.RuntimeWorkspaceRoots is not null)
        {
            throw new CodexExperimentalApiRequiredException("turn/start.runtimeWorkspaceRoots");
        }

        if (!experimentalApiEnabled && options.Environments is not null)
        {
            throw new CodexExperimentalApiRequiredException("turn/start.environments");
        }

        if (!experimentalApiEnabled && !string.IsNullOrWhiteSpace(options.PermissionProfileId))
        {
            throw new CodexExperimentalApiRequiredException("turn/start.permissions");
        }
    }
}
