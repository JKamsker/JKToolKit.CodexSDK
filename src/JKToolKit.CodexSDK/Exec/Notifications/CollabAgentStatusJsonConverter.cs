namespace JKToolKit.CodexSDK.Exec.Notifications;

/// <summary>
/// Utility methods for parsing and formatting <see cref="CollabAgentStatus"/>.
/// </summary>
public static class CollabAgentStatusJsonConverter
{
    /// <summary>
    /// Parses a wire status value into a <see cref="CollabAgentStatus"/>.
    /// </summary>
    public static CollabAgentStatus ParseOrUnknown(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CollabAgentStatus.Unknown;
        }

        value = value.Trim();

        if (value.Equals("pendingInit", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("pending_init", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("pending-init", StringComparison.OrdinalIgnoreCase))
        {
            return CollabAgentStatus.PendingInit;
        }

        if (value.Equals("running", StringComparison.OrdinalIgnoreCase))
        {
            return CollabAgentStatus.Running;
        }

        if (value.Equals("interrupted", StringComparison.OrdinalIgnoreCase))
        {
            return CollabAgentStatus.Interrupted;
        }

        if (value.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            return CollabAgentStatus.Completed;
        }

        if (value.Equals("errored", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("error", StringComparison.OrdinalIgnoreCase))
        {
            return CollabAgentStatus.Errored;
        }

        if (value.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
        {
            return CollabAgentStatus.Shutdown;
        }

        if (value.Equals("notFound", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("not_found", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("not-found", StringComparison.OrdinalIgnoreCase))
        {
            return CollabAgentStatus.NotFound;
        }

        return CollabAgentStatus.Unknown;
    }

    /// <summary>
    /// Converts a <see cref="CollabAgentStatus"/> to its wire value.
    /// </summary>
    public static string ToWireValue(CollabAgentStatus value) =>
        value switch
        {
            CollabAgentStatus.PendingInit => "pendingInit",
            CollabAgentStatus.Running => "running",
            CollabAgentStatus.Interrupted => "interrupted",
            CollabAgentStatus.Completed => "completed",
            CollabAgentStatus.Errored => "errored",
            CollabAgentStatus.Shutdown => "shutdown",
            CollabAgentStatus.NotFound => "notFound",
            _ => "unknown"
        };
}
