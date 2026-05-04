using System.Text;

namespace JKToolKit.CodexSDK.AppServer.Remote.Internal;

internal static class RemoteShell
{
    public static string Quote(string value)
    {
        if (value.Length == 0)
        {
            return "''";
        }

        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    public static string JoinQuoted(IEnumerable<string> values) =>
        string.Join(" ", values.Select(Quote));

    public static string BuildAppServerCommand(
        string codexExecutable,
        string listenUrl,
        IReadOnlyList<string>? additionalAppServerArguments)
    {
        var args = new List<string> { codexExecutable, "app-server", "--listen", listenUrl };
        if (additionalAppServerArguments is { Count: > 0 })
        {
            args.AddRange(additionalAppServerArguments);
        }

        return JoinQuoted(args);
    }

    public static string BuildDetachedStartScript(
        string id,
        string stateDirectoryExpression,
        string logFileExpression,
        string pidFileExpression,
        string listenUriPattern,
        string appServerCommand,
        TimeSpan timeout,
        string? workingDirectory)
    {
        var timeoutSeconds = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
        var script = new StringBuilder();
        script.AppendLine("set -eu");
        script.AppendLine($"id={Quote(id)}");
        script.AppendLine($"state_dir={stateDirectoryExpression}");
        script.AppendLine($"log_file={logFileExpression}");
        script.AppendLine($"pid_file={pidFileExpression}");
        script.AppendLine("mkdir -p \"$state_dir\"");
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            script.AppendLine($"cd {Quote(workingDirectory)}");
        }

        script.AppendLine($"nohup {appServerCommand} >/dev/null 2>\"$log_file\" &");
        script.AppendLine("pid=$!");
        script.AppendLine("printf '%s\\n' \"$pid\" > \"$pid_file\"");
        script.AppendLine($"deadline=$(( $(date +%s) + {timeoutSeconds} ))");
        script.AppendLine("uri=''");
        script.AppendLine("while [ \"$(date +%s)\" -le \"$deadline\" ]; do");
        script.AppendLine("  if ! kill -0 \"$pid\" 2>/dev/null; then cat \"$log_file\" >&2 || true; exit 1; fi");
        script.AppendLine($"  uri=$(sed -n 's/.*\\({listenUriPattern}\\).*/\\1/p' \"$log_file\" | tail -n 1)");
        script.AppendLine("  if [ -n \"$uri\" ]; then break; fi");
        script.AppendLine("  sleep 0.1");
        script.AppendLine("done");
        script.AppendLine("if [ -z \"$uri\" ]; then cat \"$log_file\" >&2 || true; kill \"$pid\" 2>/dev/null || true; exit 124; fi");
        script.AppendLine("printf 'CODEXSDK_ID=%s\\n' \"$id\"");
        script.AppendLine("printf 'CODEXSDK_PID=%s\\n' \"$pid\"");
        script.AppendLine("printf 'CODEXSDK_URI=%s\\n' \"$uri\"");
        script.AppendLine("printf 'CODEXSDK_STATE_DIR=%s\\n' \"$state_dir\"");
        script.AppendLine("printf 'CODEXSDK_PID_FILE=%s\\n' \"$pid_file\"");
        script.AppendLine("printf 'CODEXSDK_LOG_FILE=%s\\n' \"$log_file\"");
        return script.ToString();
    }
}
